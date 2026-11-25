using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Monta, a partir do estado REAL do jogo, os blocos de prompt usados por todas as chamadas ao Ollama
/// (dica de jogador parado e "Ajuda especial" dos NPCs), e limpa/encurta as respostas.
///
/// Por que: modelos pequenos (qwen2.5:1.5b) seguem melhor prompts curtos, com fatos concretos e
/// regras objetivas; e nem sempre respeitam o limite de tamanho, por isso a resposta é aparada aqui.
/// </summary>
public static class GameHintContext
{
    // ------------------------------------------------------------------ nível / estado

    public static PlayerController FindPlayer() => Object.FindAnyObjectByType<PlayerController>();

    public static int CurrentFloor(PlayerController player) =>
        player != null && player.dungeonGrid != null ? player.dungeonGrid.currentFloorIndex : 0;

    /// <summary>Configuração do andar atual (contexto/objetivo), editada no IdleHelpTextOllama do Player.</summary>
    public static IdleHelpTextOllama.LevelHintConfig CurrentLevel(PlayerController player)
    {
        if (player == null) return null;
        IdleHelpTextOllama idle = player.GetComponent<IdleHelpTextOllama>();
        if (idle == null) return null;
        return idle.GetLevelConfig(CurrentFloor(player));
    }

    // ------------------------------------------------------------------ etapas (dica conforme o estado)

    static bool Matches(IdleHelpTextOllama.HintCondition c, PlayerInventory inv)
    {
        if (inv == null) return c == IdleHelpTextOllama.HintCondition.Sempre;
        switch (c)
        {
            case IdleHelpTextOllama.HintCondition.SemChave: return !inv.KeyObtained;
            case IdleHelpTextOllama.HintCondition.ComChave: return inv.KeyObtained;
            case IdleHelpTextOllama.HintCondition.SemCristal: return !inv.hasCrystal;
            case IdleHelpTextOllama.HintCondition.ComCristal: return inv.hasCrystal;
            default: return true;
        }
    }

    /// <summary>Índice da 1ª etapa que vale para o estado atual (-1 = usa objetivo/dicas fixas do andar).</summary>
    public static int CurrentStageIndex(IdleHelpTextOllama.LevelHintConfig level, PlayerController player)
    {
        if (level == null || level.etapas == null) return -1;
        PlayerInventory inv = player != null ? player.GetComponent<PlayerInventory>() : null;
        for (int i = 0; i < level.etapas.Count; i++)
            if (level.etapas[i] != null && Matches(level.etapas[i].quando, inv))
                return i;
        return -1;
    }

    /// <summary>Identifica andar + etapa; muda quando o jogador troca de andar ou pega chave/cristal.</summary>
    public static string CurrentStageId(PlayerController player)
    {
        IdleHelpTextOllama.LevelHintConfig level = CurrentLevel(player);
        return CurrentFloor(player) + ":" + CurrentStageIndex(level, player);
    }

    /// <summary>
    /// Dica certa para o estado atual, da mais vaga (hintNumber 0) para a mais direta.
    /// Também devolve o objetivo da etapa.
    /// </summary>
    public static string SelectHint(IdleHelpTextOllama.LevelHintConfig level, PlayerController player,
                                    int hintNumber, out string objective)
    {
        objective = null;
        if (level == null) return null;

        int stage = CurrentStageIndex(level, player);
        System.Collections.Generic.List<string> hints = level.dicasFixas;
        objective = level.objetivo;
        if (stage >= 0)
        {
            IdleHelpTextOllama.HintStage s = level.etapas[stage];
            if (!string.IsNullOrWhiteSpace(s.objetivo)) objective = s.objetivo;
            if (s.dicas != null && s.dicas.Count > 0) hints = s.dicas;
        }
        if (hints == null || hints.Count == 0) return null;
        return hints[Mathf.Clamp(hintNumber, 0, hints.Count - 1)];
    }

    /// <summary>
    /// Prompt do modo "reescrever": o jogo já escolheu a dica; o modelo só a diz com a voz do personagem.
    /// É a tarefa mais simples possível, que modelos pequenos cumprem bem (curta e sem inventar).
    /// </summary>
    public static string RewriteSystemPrompt(string persona, int maxPersonaChars, int maxWords)
    {
        string p = ShortPersona(persona, maxPersonaChars);
        if (string.IsNullOrWhiteSpace(p))
            p = "Você é um sábio misterioso que vive nesta masmorra.";
        var sb = new StringBuilder();
        sb.AppendLine(p);
        sb.AppendLine("Tarefa: reescrever a dica recebida como uma fala sua para o jogador.");
        sb.Append("Regras: português do Brasil; 1 ou 2 frases; no máximo ").Append(maxWords)
          .Append(" palavras; mantenha o sentido da dica; não acrescente lugares, itens ou nomes; ")
          .AppendLine("comece direto pela dica, sem saudação; sem aspas, listas ou markdown.");
        return sb.ToString();
    }

    /// <summary>Bloco "FATOS DO JOGO" para o system prompt (modo GerarComContexto).</summary>
    public static string LevelFacts(IdleHelpTextOllama.LevelHintConfig level, string objective)
    {
        if (level == null) return "";
        var sb = new StringBuilder();
        sb.AppendLine($"FATOS DO JOGO (local atual: {level.nome}). São verdadeiros e têm prioridade sobre o personagem:");
        if (!string.IsNullOrWhiteSpace(level.contexto)) sb.AppendLine(level.contexto.Trim());
        if (!string.IsNullOrWhiteSpace(objective))
            sb.AppendLine("PRÓXIMO PASSO DO JOGADOR: " + objective.Trim());
        return sb.ToString();
    }

    /// <summary>Estado atual em uma linha por item: inventário e o que há em volta do jogador.</summary>
    public static string PlayerState(PlayerController player)
    {
        if (player == null) return "";
        var sb = new StringBuilder();
        sb.AppendLine("ESTADO ATUAL DO JOGADOR:");
        PlayerInventory inv = player.GetComponent<PlayerInventory>();
        if (inv != null)
        {
            sb.AppendLine($"- Tem a chave: {SimNao(inv.hasKey)}");
            sb.AppendLine($"- Tem o cristal: {SimNao(inv.hasCrystal)}");
            sb.AppendLine($"- Aprendeu a magia: {SimNao(inv.hasLearnedMagic)}");
        }
        string around = DescribeSurroundings(player);
        if (!string.IsNullOrEmpty(around)) sb.AppendLine("- Arredores: " + around);
        return sb.ToString();
    }

    /// <summary>Quanto revelar: a 1ª dica é vaga e as seguintes ficam cada vez mais diretas.</summary>
    public static string DetailInstruction(int hintNumber)
    {
        switch (hintNumber)
        {
            case 0: return "- Primeira dica: seja vago e misterioso, só aponte a direção geral.";
            case 1: return "- O jogador já recebeu uma dica e continua travado: seja mais concreto.";
            default: return "- O jogador está travado há muito tempo: diga claramente o que fazer a seguir.";
        }
    }

    /// <summary>Regras de formato, iguais para todas as chamadas.</summary>
    public static string ResponseRules(int maxWords)
    {
        var sb = new StringBuilder();
        sb.AppendLine("REGRAS DA RESPOSTA (obrigatórias):");
        sb.AppendLine($"- Português do Brasil. No máximo 2 frases curtas e {maxWords} palavras no total.");
        sb.AppendLine("- Dê UMA dica prática sobre o próximo passo, usando só os FATOS DO JOGO e o ESTADO ATUAL.");
        sb.AppendLine("- Não invente lugares, itens, personagens ou missões. Não cite outros jogos, planetas ou cidades.");
        sb.AppendLine("- Comece direto pela dica: sem saudação, sem consolar o jogador, sem repetir a pergunta.");
        sb.AppendLine("- Se o jogador já tem um item, não mande buscá-lo de novo.");
        sb.AppendLine("- Sem listas, aspas, emojis, markdown ou explicações. Responda só a fala do personagem.");
        return sb.ToString();
    }

    /// <summary>
    /// Persona do NPC reduzida: só a parte inicial (personalidade e o que ele sabe),
    /// cortada em fim de frase. Personas longas confundem o modelo pequeno.
    /// </summary>
    public static string ShortPersona(string persona, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(persona)) return "";
        string p = Regex.Replace(persona.Trim(), @"\s+", " ");
        if (maxChars <= 0 || p.Length <= maxChars) return p;
        string cut = p.Substring(0, maxChars);
        int end = cut.LastIndexOfAny(new[] { '.', '!', '?' });
        return end > maxChars / 3 ? cut.Substring(0, end + 1) : cut.TrimEnd() + "...";
    }

    // ------------------------------------------------------------------ limpeza da resposta

    /// <summary>
    /// Deixa a resposta pronta para a caixa de diálogo: tira markdown/aspas/rótulos ("NPC:"),
    /// junta as linhas e mantém no máximo <paramref name="maxSentences"/> frases e <paramref name="maxChars"/> caracteres.
    /// </summary>
    public static string CleanResponse(string text, int maxSentences = 2, int maxChars = 180)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        string t = text.Trim();

        t = Regex.Replace(t, @"<think>.*?</think>", "", RegexOptions.Singleline); // modelos que "pensam"

        // O Qwen às vezes troca para chinês no meio da frase: descarta (quem chama usa a dica fixa)
        if (Regex.IsMatch(t, @"[぀-ヿ㐀-鿿가-힯＀-￯]"))
            return null;
        t = Regex.Replace(t, @"[*_#`>~]+", "");                                     // markdown
        t = Regex.Replace(t, @"^\s*(?:[-•]|\d+[.)])\s+", "", RegexOptions.Multiline); // marcadores de lista
        t = Regex.Replace(t, @"^\s*\p{L}+(?: \p{L}+)?:\s+", "");                        // "Mago: ..." / "Resposta: ..."
        t = Regex.Replace(t, @"\s+", " ").Trim();
        t = t.Trim('"', '\'', '“', '”', '«', '»', ' ');

        // Mantém só as primeiras frases completas (descarta frase cortada pelo limite de tokens)
        MatchCollection sentences = Regex.Matches(t, @"[^.!?…]+[.!?…]+[""”']?\s*");
        if (sentences.Count > 0)
        {
            int keep = maxSentences > 0 ? Mathf.Min(maxSentences, sentences.Count) : sentences.Count;
            var sb = new StringBuilder();
            for (int i = 0; i < keep; i++) sb.Append(sentences[i].Value);
            t = sb.ToString().Trim();
        }

        // Limite de caracteres, cortando em palavra inteira
        if (maxChars > 0 && t.Length > maxChars)
        {
            string cut = t.Substring(0, maxChars);
            int sentenceEnd = cut.LastIndexOfAny(new[] { '.', '!', '?' });
            if (sentenceEnd > maxChars / 2) t = cut.Substring(0, sentenceEnd + 1);
            else
            {
                int space = cut.LastIndexOf(' ');
                t = (space > 0 ? cut.Substring(0, space) : cut).TrimEnd(',', ';', ':', ' ') + "...";
            }
        }
        return t;
    }

    // ------------------------------------------------------------------ arredores

    static string SimNao(bool v) => v ? "sim" : "não";

    /// <summary>O que há à frente, à esquerda, à direita e atrás do jogador.</summary>
    public static string DescribeSurroundings(PlayerController player)
    {
        if (player == null || player.dungeonGrid == null) return null;

        Vector3 f = player.transform.forward;
        Vector2Int fwd = Mathf.Abs(f.x) > Mathf.Abs(f.z)
            ? new Vector2Int(f.x > 0 ? 1 : -1, 0)
            : new Vector2Int(0, f.z > 0 ? 1 : -1);
        Vector2Int right = new Vector2Int(fwd.y, -fwd.x);

        string Look(Vector2Int d) => TileName(player.dungeonGrid.GetTileType(player.gridX + d.x, player.gridZ + d.y));

        return $"à frente {Look(fwd)}; à esquerda {Look(-right)}; à direita {Look(right)}; atrás {Look(-fwd)}.";
    }

    public static string TileName(TileType tile)
    {
        switch (tile)
        {
            case TileType.Floor: return "corredor livre";
            case TileType.Wall: return "parede";
            case TileType.DoorHidden: return "parede";   // passagem secreta: não revelar pelo estado
            case TileType.StairsUp: return "escada para subir";
            case TileType.StairsDown: return "escada para descer";
            case TileType.DoorNormal: return "porta comum";
            case TileType.DoorKey: return "porta trancada (precisa de chave)";
            case TileType.DoorMagic: return "porta mágica (precisa do cristal ou de magia)";
            case TileType.DoorOpen: return "porta aberta";
            case TileType.Trap: return "chão suspeito";
            case TileType.Pit: return "buraco";
            case TileType.NPCChest: return "um baú";
            case TileType.Dragon: return "uma criatura enorme e perigosa";
            case TileType.NPCCristal: return "um guardião";
            case TileType.NPC:
            case TileType.NPC2:
            case TileType.NPC3:
            case TileType.NPC4:
            case TileType.NPCQuestion: return "um personagem";
            case TileType.EndGame: return "a saída";
            default: return tile.ToString();
        }
    }
}

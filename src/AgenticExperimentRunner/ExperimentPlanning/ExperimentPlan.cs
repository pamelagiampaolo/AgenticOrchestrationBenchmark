using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AgenticExperimentRunner.Coordination;
using AgenticExperimentRunner.SessionExecution;

namespace AgenticExperimentRunner.ExperimentPlanning;

/// <summary>Slot immutabile della matrice ufficiale.</summary>
/// <param name="OrderPosition">Posizione globale, da 1 a 54.</param>
/// <param name="BlockPosition">Posizione del blocco task–ripetizione, da 1 a 18.</param>
/// <param name="SlotId">Identificatore deterministico dello slot.</param>
/// <param name="TaskId">Task T1–T6.</param>
/// <param name="Architecture">Architettura sperimentale.</param>
/// <param name="Repetition">Ripetizione da 1 a 3.</param>
/// <param name="RunKind">Sempre <see cref="ExperimentRunKind.Official"/>.</param>
public sealed record ExperimentPlanSlot(
    int OrderPosition,
    int BlockPosition,
    string SlotId,
    string TaskId,
    string Architecture,
    int Repetition,
    ExperimentRunKind RunKind);

/// <summary>Piano completo e riproducibile delle 54 run ufficiali.</summary>
/// <param name="SchemaVersion">Versione del formato.</param>
/// <param name="Seed">Object ID Git completo del commit finale di freeze.</param>
/// <param name="Algorithm">Identificatore versionato dell'algoritmo.</param>
/// <param name="Slots">Slot nell'ordine di esecuzione.</param>
public sealed record ExperimentPlan(
    int SchemaVersion,
    string Seed,
    string Algorithm,
    IReadOnlyList<ExperimentPlanSlot> Slots)
{
    /// <summary>Versione corrente del formato.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Algoritmo corrente, parte delle condizioni congelate.</summary>
    public const string CurrentAlgorithm = "sha256-blocked-v1";
}

/// <summary>Genera l'ordine ufficiale bilanciato a blocchi dall'object ID Git del freeze.</summary>
public static partial class ExperimentPlanGenerator
{
    private static readonly string[] TaskIds = ["T1", "T2", "T3", "T4", "T5", "T6"];
    private static readonly string[] Architectures =
        [SingleAgentSessionRunner.ArchitectureName, StaticHierarchyPlan.ArchitectureName, HierarchicalRunRunner.ArchitectureName];

    /// <summary>
    /// Genera i 54 slot. I blocchi sono ordinati tramite SHA-256 di
    /// <c>seed|block|task|repetition</c>; al loro interno le architetture tramite
    /// SHA-256 di <c>seed|task|repetition|architecture</c>.
    /// </summary>
    /// <param name="seed">Object ID Git completo del commit finale, in esadecimale minuscolo.</param>
    /// <returns>Piano valido e deterministico.</returns>
    /// <exception cref="ArgumentException">Il seed non è un object ID Git completo.</exception>
    public static ExperimentPlan Generate(string seed)
    {
        if (!IsValidSeed(seed))
        {
            throw new ArgumentException("Il seed deve essere un object ID Git completo di 40 o 64 caratteri esadecimali minuscoli.", nameof(seed));
        }

        var blocks = (
            from taskId in TaskIds
            from repetition in Enumerable.Range(1, 3)
            let key = Hash($"{seed}|block|{taskId}|{repetition}")
            orderby key, taskId, repetition
            select (taskId, repetition)).ToList();

        var slots = new List<ExperimentPlanSlot>(54);
        for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
        {
            (string taskId, int repetition) = blocks[blockIndex];
            IEnumerable<string> orderedArchitectures = Architectures
                .OrderBy(architecture => Hash($"{seed}|{taskId}|{repetition}|{architecture}"), StringComparer.Ordinal)
                .ThenBy(architecture => architecture, StringComparer.Ordinal);
            foreach (string architecture in orderedArchitectures)
            {
                slots.Add(new ExperimentPlanSlot(
                    slots.Count + 1,
                    blockIndex + 1,
                    $"official-{taskId.ToLowerInvariant()}-{architecture}-r{repetition}",
                    taskId,
                    architecture,
                    repetition,
                    ExperimentRunKind.Official));
            }
        }

        var plan = new ExperimentPlan(ExperimentPlan.CurrentSchemaVersion, seed, ExperimentPlan.CurrentAlgorithm, slots);
        IReadOnlyList<string> errors = ExperimentPlanValidator.FindErrors(plan);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Il generatore ha prodotto un piano non valido: {string.Join("; ", errors)}");
        }

        return plan;
    }

    /// <summary>Indica se il valore è un object ID Git completo supportato.</summary>
    /// <param name="seed">Valore da verificare.</param>
    /// <returns><see langword="true"/> per 40 o 64 cifre esadecimali minuscole.</returns>
    public static bool IsValidSeed(string? seed) => seed is not null && GitObjectIdPattern().IsMatch(seed);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    [GeneratedRegex("^(?:[0-9a-f]{40}|[0-9a-f]{64})$", RegexOptions.CultureInvariant)]
    private static partial Regex GitObjectIdPattern();
}

/// <summary>Verifica struttura, copertura e bilanciamento di un piano ufficiale.</summary>
public static class ExperimentPlanValidator
{
    /// <summary>Restituisce tutte le violazioni osservate senza modificare il piano.</summary>
    /// <param name="plan">Piano da verificare.</param>
    /// <returns>Lista vuota se il piano è valido.</returns>
    public static IReadOnlyList<string> FindErrors(ExperimentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var errors = new List<string>();
        if (plan.SchemaVersion != ExperimentPlan.CurrentSchemaVersion)
        {
            errors.Add($"schemaVersion inattesa: {plan.SchemaVersion}");
        }

        if (plan.Algorithm != ExperimentPlan.CurrentAlgorithm)
        {
            errors.Add($"algoritmo inatteso: '{plan.Algorithm}'");
        }

        if (!ExperimentPlanGenerator.IsValidSeed(plan.Seed))
        {
            errors.Add("seed non valido");
        }

        if (plan.Slots is null)
        {
            errors.Add("lista degli slot mancante");
            return errors;
        }

        if (plan.Slots.Count != 54)
        {
            errors.Add($"numero slot diverso da 54: {plan.Slots.Count}");
        }

        if (!plan.Slots.Select(slot => slot.OrderPosition).SequenceEqual(Enumerable.Range(1, plan.Slots.Count)))
        {
            errors.Add("posizioni globali non consecutive");
        }

        if (plan.Slots.Select(slot => slot.SlotId).Distinct(StringComparer.Ordinal).Count() != plan.Slots.Count)
        {
            errors.Add("slotId duplicati");
        }

        if (!plan.Slots.Select(slot => slot.BlockPosition).Distinct().Order().SequenceEqual(Enumerable.Range(1, 18)))
        {
            errors.Add("posizioni dei blocchi diverse da 1–18");
        }

        var expectedArchitectures = new HashSet<string>(
            [SingleAgentSessionRunner.ArchitectureName, StaticHierarchyPlan.ArchitectureName, HierarchicalRunRunner.ArchitectureName],
            StringComparer.Ordinal);
        foreach (IGrouping<int, ExperimentPlanSlot> block in plan.Slots.GroupBy(slot => slot.BlockPosition))
        {
            if (block.Count() != 3 || !expectedArchitectures.SetEquals(block.Select(slot => slot.Architecture)))
            {
                errors.Add($"blocco {block.Key} non contiene una volta ciascuna architettura");
            }

            if (block.Select(slot => (slot.TaskId, slot.Repetition)).Distinct().Count() != 1)
            {
                errors.Add($"blocco {block.Key} mescola task o ripetizioni");
            }
        }

        var expectedCombinations = (
            from task in Enumerable.Range(1, 6).Select(value => $"T{value}")
            from repetition in Enumerable.Range(1, 3)
            from architecture in expectedArchitectures
            select (task, repetition, architecture)).ToHashSet();
        var actualCombinations = plan.Slots
            .Select(slot => (slot.TaskId, slot.Repetition, slot.Architecture))
            .ToHashSet();
        if (!expectedCombinations.SetEquals(actualCombinations))
        {
            errors.Add("copertura task–ripetizione–architettura incompleta o inattesa");
        }

        if (plan.Slots.Any(slot => slot.RunKind != ExperimentRunKind.Official))
        {
            errors.Add("il piano contiene run non ufficiali");
        }

        return errors;
    }
}

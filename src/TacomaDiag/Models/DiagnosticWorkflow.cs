namespace TacomaDiag.Models;

public sealed class DiagnosticWorkflow
{
    public string Name { get; init; } = "";
    public string Objective { get; init; } = "";
    public string WhenToUse { get; init; } = "";
    public IReadOnlyList<WorkflowStep> Steps { get; init; } = Array.Empty<WorkflowStep>();

    public override string ToString() => Name;
}

public sealed class WorkflowStep
{
    public int Number { get; init; }
    public string Action { get; init; } = "";
    public string PassCondition { get; init; } = "";
    public string NextIfFail { get; init; } = "";
}

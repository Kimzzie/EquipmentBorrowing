namespace EquipmentBorrowing.Tests.Infrastructure;

/// <summary>
/// Arms the application's EQUIPMENT_BORROWING_FAULT seam for the duration of a using-block and
/// ALWAYS disarms it afterwards, even if the test fails, so one test can never leak a fault into
/// the next. (The seam only acts on a database whose name ends in "_test"; ours does.)
/// The variable is process-wide, which is why test parallelization is disabled for this assembly.
/// </summary>
public sealed class FaultScope : IDisposable
{
    public const string VariableName = "EQUIPMENT_BORROWING_FAULT";

    private FaultScope(string point)
    {
        Environment.SetEnvironmentVariable(VariableName, point);
    }

    /// <summary>The write was SENT, then the failure happens BEFORE the commit: the transaction rolls back.</summary>
    public static FaultScope BeforeCommit() => new("BeforeCommit");

    /// <summary>The commit SUCCEEDS, then the acknowledgement is "lost": the outcome is unknown to the caller.</summary>
    public static FaultScope AfterCommit() => new("AfterCommit");

    public void Dispose() => Environment.SetEnvironmentVariable(VariableName, null);
}

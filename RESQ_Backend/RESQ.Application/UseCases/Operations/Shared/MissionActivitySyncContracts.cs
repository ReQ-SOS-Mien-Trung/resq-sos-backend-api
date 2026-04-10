namespace RESQ.Application.UseCases.Operations.Shared;

public static class MissionActivitySyncOutcomes
{
    public const string Processing = "processing";
    public const string Applied = "applied";
    public const string Duplicate = "duplicate";
    public const string Conflict = "conflict";
    public const string Rejected = "rejected";
    public const string Failed = "failed";
}

public static class MissionActivitySyncErrorCodes
{
    public const string ExceptionDataKey = "MissionActivitySyncErrorCode";

    public const string ActivityNotFound = "ACTIVITY_NOT_FOUND";
    public const string MissionActivityMismatch = "MISSION_ACTIVITY_MISMATCH";
    public const string ForbiddenTeamMismatch = "FORBIDDEN_TEAM_MISMATCH";
    public const string InvalidStatusTransition = "INVALID_STATUS_TRANSITION";
    public const string ActivitySequenceBlocked = "ACTIVITY_SEQUENCE_BLOCKED";
    public const string AlreadyAtTargetStatus = "ALREADY_AT_TARGET_STATUS";
    public const string BaseStatusMismatch = "BASE_STATUS_MISMATCH";
    public const string ServerError = "SERVER_ERROR";

    public static TException WithCode<TException>(TException exception, string code)
        where TException : Exception
    {
        exception.Data[ExceptionDataKey] = code;
        return exception;
    }

    public static string? TryGet(Exception exception) => exception.Data[ExceptionDataKey] as string;
}
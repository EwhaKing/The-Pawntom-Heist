namespace Pawntom.Stage
{
    public enum StageLayoutIssueCode
    {
        MissingLayout,
        InvalidStageNumber,
        InvalidFloorNumber,
        TooFewDockingCandidates,
        InvalidRequiredKeyCount,
        RequiredKeysExceedCandidates,
        TooFewWaypoints,
        MissingId,
        DuplicateId,
        InvalidSpawnChance,
        NoAllowedItems,
        UnknownRouteReference,
        UnknownZoneReference,
        InvalidK9Count,
        K9CountExceedsRoutes,
    }

    /// <summary>
    /// Error = 데이터가 설계를 어긴다(고쳐야 한다). Warning = 돌아가지만 의도와 다를 수 있다(예: 경로가 모자라 K-9 이 덜 나옴).
    /// </summary>
    public enum StageLayoutIssueSeverity
    {
        Error = 0,
        Warning = 1,
    }

    /// <summary>레벨 데이터 검증 결과 하나.</summary>
    public readonly struct StageLayoutIssue
    {
        public readonly StageLayoutIssueCode Code;
        public readonly StageLayoutIssueSeverity Severity;

        /// <summary>항목 분류(Docking, Door …). 레이아웃 전체 오류면 "Layout".</summary>
        public readonly string Category;

        /// <summary>문제가 된 항목 Id. 없으면 빈 문자열.</summary>
        public readonly string EntryId;

        public readonly string Message;

        public StageLayoutIssue(StageLayoutIssueCode code, string category, string entryId, string message)
            : this(code, StageLayoutIssueSeverity.Error, category, entryId, message)
        {
        }

        public StageLayoutIssue(StageLayoutIssueCode code, StageLayoutIssueSeverity severity, string category, string entryId, string message)
        {
            Code = code;
            Severity = severity;
            Category = category ?? string.Empty;
            EntryId = entryId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString()
        {
            string head = "[" + Severity + ":" + Code + "] " + Category;
            return EntryId.Length == 0 ? head + ": " + Message : head + " '" + EntryId + "': " + Message;
        }
    }
}

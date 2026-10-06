namespace PTL.Core.Lookup;
// Keyless projections backing the three Scheme "Details" tab dropdowns that legacy
// Scheme.aspx renders via DropDownSchedule / DropDownScheduleCode / DropDownDayOfWeek.
public class ScheduleEntity
{
    public Guid ScheduleId { get; set; }
    public string Schedule { get; set; } = string.Empty;
}

public class ScheduleCodeEntity
{
    public Guid ScheduleCodeId { get; set; }
    public string ScheduleCode { get; set; } = string.Empty;
}

// spgaMonthlyDistributionInfo - only the year/month pair is modelled; the Scheme screen uses it to
// decide which distribution months have already been initialised and are therefore locked.
public class MonthlyDistributionEntity
{
    public int YearId { get; set; }
    public int MonthId { get; set; }
}

public class DayEntity
{
    public Guid DayId { get; set; }
    public string Day { get; set; } = string.Empty;
}

// spgaPTNumbers returns every scheme's identifier across all years. Legacy loads the same list
// into mPTNumbers and uses it for both the client-side duplicate warning and the server-side
// UniquePTNumberValidator on Scheme.aspx.
public class PTNumberEntity
{
    public Guid SchemeId { get; set; }
    public string Identifier { get; set; } = string.Empty;
}

// A person selectable as a Test Consultant or an Assessor on the Scheme screen. The id is
// tblUsers.fldUserId for internal users and tblExternalTestConsultant.fldExternalTestConsultantId
// for external test consultants - spgaUserAllTestConsultant aliases both as fldUserId, and
// tblScheme.fldTestConsultant1..3 / fldAssessor1..4 store that value with no FK constraint.
public class SchemeUserEntity
{
    public Guid UserId { get; set; }
    public string FriendlyName { get; set; } = string.Empty;
    public bool IsInactive { get; set; }

    // Set by the repository from which spgaUserAllTestConsultant result set the row came.
    // Only internal consultants may be the Primary Test Consultant.
    public bool IsExternal { get; set; }
}

// A row from any of the five spgXxxTypeByYearId procedures behind the Tests tab "Add" dropdowns.
public class SchemeItemTypeEntity
{
    public Guid ItemTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool NoLongerInUse { get; set; }
}

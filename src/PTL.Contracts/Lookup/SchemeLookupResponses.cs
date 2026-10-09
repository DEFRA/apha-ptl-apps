namespace PTL.Contracts.Lookup;

// Reference lists backing the Scheme "Details" tab dropdowns (legacy DropDownSchedule,
// DropDownScheduleCode and DropDownDayOfWeek on Scheme.aspx).
public sealed record ScheduleResponse(Guid ScheduleId, string Schedule);

public sealed record ScheduleCodeResponse(Guid ScheduleCodeId, string ScheduleCode);

public sealed record DayResponse(Guid DayId, string Day);

// A person selectable as a Test Consultant or Assessor on the Scheme screen. IsExternal is only
// ever true for external test consultants, who cannot be the Primary Test Consultant.
public sealed record SchemeUserResponse(Guid UserId, string FriendlyName, bool IsExternal);

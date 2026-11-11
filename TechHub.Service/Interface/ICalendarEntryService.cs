using System;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Calendar;

namespace TechHub.Service.Interface
{
	public interface ICalendarEntryService
	{
		BaseResponse GetEntryTypes();

		/// <summary>Entries visible to the caller (school-wide plus their own classes/subjects/children), optionally within a date range.</summary>
		Task<BaseResponse> ListAsync(CalendarEntryQueryViewModel query, AuthenticatedUserClaims claims);

		/// <summary>One entry with its full change history.</summary>
		Task<BaseResponse> GetAsync(Guid entryId, AuthenticatedUserClaims claims);

		Task<BaseResponse> CreateAsync(CreateCalendarEntryViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> UpdateAsync(Guid entryId, UpdateCalendarEntryViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> DeleteAsync(Guid entryId, AuthenticatedUserClaims claims);
	}
}

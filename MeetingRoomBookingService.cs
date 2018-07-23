using System.Threading.Tasks;

namespace TechClubAssistantBot
{
    public interface IMeetingRoomBookingService
    {
        Task<BookingResult> BookMeetingRoomAsync(BookingRequest request);
    }

    public class BookingRequest
    {
        public string MeetingRoom { get; set; }
        public string Time { get; set; }
        public string Duration { get; set; }
    }

    public class BookingResult
    {

    }

    public class MeetingRoomBookingService : IMeetingRoomBookingService
    {
        public async Task<BookingResult> BookMeetingRoomAsync(BookingRequest request)
        {
            System.Diagnostics.Trace.WriteLine($">>>> {request.MeetingRoom}, {request.Time}, {request.Duration}");

            return new BookingResult();
        }
    }
}

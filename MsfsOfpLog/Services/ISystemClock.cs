using System;

namespace MsfsOfpLog.Services
{
    public interface ISystemClock
    {
        DateTime Now { get; }
    }
    
    public class SystemClock : ISystemClock
    {
        public DateTime Now => DateTime.UtcNow;
    }
    
    public class TestSystemClock : ISystemClock
    {
        private DateTime _currentTime;
        
        public TestSystemClock(DateTime startTime)
        {
            // Ensure the start time is treated as UTC
            _currentTime = startTime.Kind == DateTimeKind.Utc ? startTime : DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
        }
        
        public DateTime Now => _currentTime;
        
        public void SetTime(DateTime time)
        {
            // Ensure the time is treated as UTC
            _currentTime = time.Kind == DateTimeKind.Utc ? time : DateTime.SpecifyKind(time, DateTimeKind.Utc);
        }
        
        public void AddMinutes(int minutes)
        {
            _currentTime = _currentTime.AddMinutes(minutes);
        }
        
        public void AddSeconds(int seconds)
        {
            _currentTime = _currentTime.AddSeconds(seconds);
        }
    }
}

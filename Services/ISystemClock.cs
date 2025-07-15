using System;

namespace MsfsOfpLog.Services
{
    public interface ISystemClock
    {
        DateTime Now { get; }
    }
    
    public class SystemClock : ISystemClock
    {
        public DateTime Now => DateTime.Now;
    }
    
    public class TestSystemClock : ISystemClock
    {
        private DateTime _currentTime;
        
        public TestSystemClock(DateTime startTime)
        {
            _currentTime = startTime;
        }
        
        public DateTime Now => _currentTime;
        
        public void SetTime(DateTime time)
        {
            _currentTime = time;
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

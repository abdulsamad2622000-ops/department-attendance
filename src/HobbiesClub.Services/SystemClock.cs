using HobbiesClub.Core.Interfaces;

namespace HobbiesClub.Services;

public class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}

using OOAD.Domain.Common;

namespace OOAD.Domain.Entities;

public class TrainerAvailability : BaseEntity
{
    public Guid TrainerId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsBooked { get; set; }

    // Navigation properties
    public Trainer Trainer { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

using OOAD.Application.Common.Interfaces;
using OOAD.Domain.Entities;
using OOAD.Domain.Enums;
using OOAD.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OOAD.Infrastructure.Persistence.Seed;

/// <summary>
/// Bộ khởi tạo và cứu hộ Database tự động (Defense Readiness - Task 1-A).
/// Dù giảng viên có xóa/drop toàn bộ Database khi chấm bài, khi API khởi động lại
/// hệ thống sẽ tự động chạy Migration / tạo lại 25 bảng từ AppDbContext (Fluent API) và nạp đầy đủ Seed Data (1-A).
/// </summary>
public class AppDbContextInitialiser
{
    private readonly ILogger<AppDbContextInitialiser> _logger;
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;

    public AppDbContextInitialiser(
        ILogger<AppDbContextInitialiser> logger,
        AppDbContext context,
        ITokenService tokenService)
    {
        _logger = logger;
        _context = context;
        _tokenService = tokenService;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            _logger.LogInformation("Đang kiểm tra và khởi tạo AppDbContext (25 Entities Fluent API)...");

            var migrations = _context.Database.GetMigrations();
            if (migrations.Any())
            {
                await _context.Database.MigrateAsync();
            }
            else
            {
                await _context.Database.EnsureCreatedAsync();
            }

            await EnsureNullableEquipmentIssueReportedByAsync();

            _logger.LogInformation("Khởi tạo cấu trúc 25 bảng Database thành công.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MigrateAsync gặp xung đột schema cũ, đang tự động tái tạo từ AppDbContext Fluent API...");
            try
            {
                await _context.Database.EnsureDeletedAsync();
                var migrations = _context.Database.GetMigrations();
                if (migrations.Any())
                {
                    await _context.Database.MigrateAsync();
                }
                else
                {
                    await _context.Database.EnsureCreatedAsync();
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Đã xảy ra lỗi trong quá trình khởi tạo Database.");
                throw;
            }
        }
    }

    private async Task EnsureNullableEquipmentIssueReportedByAsync()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'Equipment_Issues')
                      AND name = 'Reported_By'
                      AND is_nullable = 0
                )
                BEGIN
                    ALTER TABLE [Equipment_Issues] ALTER COLUMN [Reported_By] uniqueidentifier NULL;
                END");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bỏ qua bước cập nhật cột Equipment_Issues.Reported_By.");
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Đã xảy ra lỗi khi Seed dữ liệu ban đầu (1-A).");
            throw;
        }
    }

    private async Task TrySeedAsync()
    {
        // 1. Seed Chi nhánh (Branches)
        if (!await _context.Branches.AnyAsync())
        {
            _logger.LogInformation("Đang tiến hành Seed danh sách chi nhánh TP.HCM...");

            var branches = new List<Branch>
            {
                new()
                {
                    Name = "DF Gym Center - Chi nhánh Bến Nghé",
                    District = "Quận 1",
                    Address = "12 Lê Thánh Tôn, Phường Bến Nghé, Quận 1, TP.HCM",
                    PhoneNumber = "028 3822 1111",
                    OpenTime = new TimeSpan(5, 30, 0),
                    CloseTime = new TimeSpan(22, 30, 0),
                    OperatingHours = "05:30 - 22:30",
                    Status = "ACTIVE",
                    IsActive = true
                },
                new()
                {
                    Name = "DF Gym Center - Chi nhánh Phú Mỹ Hưng",
                    District = "Quận 7",
                    Address = "105 Tôn Dật Tiên, Tân Phong, Quận 7, TP.HCM",
                    PhoneNumber = "028 5410 2222",
                    OpenTime = new TimeSpan(5, 30, 0),
                    CloseTime = new TimeSpan(22, 0, 0),
                    OperatingHours = "05:30 - 22:00",
                    Status = "ACTIVE",
                    IsActive = true
                },
                new()
                {
                    Name = "DF Gym Center - Chi nhánh Sân Bay",
                    District = "Tân Bình",
                    Address = "18 Hồng Hà, Phường 2, Quận Tân Bình, TP.HCM",
                    PhoneNumber = "028 3848 3333",
                    OpenTime = new TimeSpan(5, 0, 0),
                    CloseTime = new TimeSpan(23, 0, 0),
                    OperatingHours = "05:00 - 23:00",
                    Status = "ACTIVE",
                    IsActive = true
                },
                new()
                {
                    Name = "DF Gym Center - Chi nhánh Landmark",
                    District = "Bình Thạnh",
                    Address = "208 Nguyễn Hữu Cảnh, Phường 22, Bình Thạnh, TP.HCM",
                    PhoneNumber = "028 3512 4444",
                    OpenTime = new TimeSpan(5, 30, 0),
                    CloseTime = new TimeSpan(22, 0, 0),
                    OperatingHours = "05:30 - 22:00",
                    Status = "ACTIVE",
                    IsActive = true
                }
            };

            await _context.Branches.AddRangeAsync(branches);
            await _context.SaveChangesAsync();
        }

        var firstBranch = await _context.Branches.FirstAsync();

        // 2. Seed Users (Admin, Staff/Lễ tân, Trainer/HLV, Members/Hội viên) — Mật khẩu mặc định: 123456
        if (!await _context.Users.AnyAsync())
        {
            _logger.LogInformation("Đang tiến hành Seed tài khoản mẫu (Admin, Lễ tân, HLV, Hội viên)...");
            var defaultPasswordHash = _tokenService.HashPassword("123456");

            var adminUser = new User
            {
                Email = "admin@ooad.vn",
                UserName = "admin",
                FullName = "Quản trị viên OOAD",
                PasswordHash = defaultPasswordHash,
                Phone = "0909000001",
                Role = UserRole.Admin,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            var staffUser = new User
            {
                Email = "letan@ooad.vn",
                UserName = "letan_q1",
                FullName = "Phạm Thu Hà (Lễ tân)",
                PasswordHash = defaultPasswordHash,
                Phone = "0909000002",
                Role = UserRole.Staff,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            var staffProfile = new Staff
            {
                UserId = staffUser.Id,
                BranchId = firstBranch.Id,
                Position = "Receptionist"
            };

            var trainerUser = new User
            {
                Email = "pt@ooad.vn",
                UserName = "hlv_minhkhoi",
                FullName = "Trần Minh Khôi (HLV)",
                PasswordHash = defaultPasswordHash,
                Phone = "0909000003",
                Role = UserRole.Trainer,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            var trainerProfile = new Trainer
            {
                UserId = trainerUser.Id,
                BranchId = firstBranch.Id,
                Specialty = "Strength, Hypertrophy, HIIT",
                ExperienceYears = 6,
                Bio = "Huấn luyện viên chứng chỉ NASM-CPT với hơn 6 năm kinh nghiệm huấn luyện thực chiến."
            };

            var memberUser1 = new User
            {
                Email = "hoivien@ooad.vn",
                UserName = "hoivien",
                FullName = "Nguyễn Hoàng Nam",
                PasswordHash = defaultPasswordHash,
                Phone = "0901234567",
                Role = UserRole.Member,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            var member1 = new Member
            {
                UserId = memberUser1.Id,
                Address = "45 Nguyễn Huệ, Quận 1, TP.HCM",
                DateOfBirth = new DateTime(1998, 5, 15),
                Gender = "male"
            };
            var card1 = new MemberCard
            {
                MemberId = member1.Id,
                CardCode = "DF-1001",
                Status = "ACTIVE"
            };

            var memberUser2 = new User
            {
                Email = "minhanh@ooad.vn",
                UserName = "minhanh",
                FullName = "Lê Minh Anh",
                PasswordHash = defaultPasswordHash,
                Phone = "0902345678",
                Role = UserRole.Member,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            var member2 = new Member
            {
                UserId = memberUser2.Id,
                Address = "120 Nguyễn Thị Thập, Quận 7, TP.HCM",
                DateOfBirth = new DateTime(2000, 8, 20),
                Gender = "female"
            };
            var card2 = new MemberCard
            {
                MemberId = member2.Id,
                CardCode = "DF-1002",
                Status = "ACTIVE"
            };

            await _context.Users.AddRangeAsync(adminUser, staffUser, trainerUser, memberUser1, memberUser2);
            await _context.Staff.AddAsync(staffProfile);
            await _context.Trainers.AddAsync(trainerProfile);
            await _context.Members.AddRangeAsync(member1, member2);
            await _context.MemberCards.AddRangeAsync(card1, card2);
            await _context.SaveChangesAsync();

            // 3. Seed Gói hội viên (MembershipPlans) & Đăng ký gói (Memberships gồm Branch_Id)
            if (!await _context.MembershipPlans.AnyAsync())
            {
                var planStandard = new MembershipPlan
                {
                    Name = "Gói Standard 1 Tháng",
                    DurationDays = 30,
                    Price = 790_000m,
                    PriorityLevel = 1,
                    MaxBookingsPerMonth = 12,
                    MaxFreezeTimes = 0,
                    MaxFreezeDays = 0,
                    IsAllBranches = false,
                    PlanType = "MEMBERSHIP",
                    PtSessions = 0
                };

                var planVip = new MembershipPlan
                {
                    Name = "Gói VIP All-Pass 6 Tháng",
                    DurationDays = 180,
                    Price = 4_200_000m,
                    PriorityLevel = 3,
                    MaxBookingsPerMonth = 60,
                    MaxFreezeTimes = 2,
                    MaxFreezeDays = 30,
                    IsAllBranches = true,
                    PlanType = "MEMBERSHIP",
                    PtSessions = 5
                };

                await _context.MembershipPlans.AddRangeAsync(planStandard, planVip);
                await _context.PlanBranches.AddAsync(new PlanBranch
                {
                    PlanId = planStandard.Id,
                    BranchId = firstBranch.Id
                });

                var activeMembership = new Membership
                {
                    MemberId = member1.Id,
                    PlanId = planVip.Id,
                    BranchId = firstBranch.Id,
                    StartDate = DateTime.UtcNow.AddDays(-15),
                    EndDate = DateTime.UtcNow.AddDays(165),
                    RemainingSessions = 5,
                    Status = MembershipStatus.Active
                };

                var payment = new Payment
                {
                    MembershipId = activeMembership.Id,
                    Amount = planVip.Price,
                    Method = "TRANSFER",
                    Status = PaymentStatus.Completed,
                    GatewayTxnCode = "TXN-SEED-001",
                    CreatedAt = DateTime.UtcNow.AddDays(-15),
                    PaidAt = DateTime.UtcNow.AddDays(-15)
                };

                await _context.Memberships.AddAsync(activeMembership);
                await _context.Payments.AddAsync(payment);
                await _context.SaveChangesAsync();
            }

            // 4. Seed MetricTypes
            if (!await _context.MetricTypes.AnyAsync())
            {
                await _context.MetricTypes.AddRangeAsync(
                    new MetricType { Code = "WEIGHT", Name = "Cân nặng", Unit = "kg", IsActive = true },
                    new MetricType { Code = "BMI", Name = "Chỉ số BMI", Unit = "kg/m2", IsActive = true },
                    new MetricType { Code = "BODY_FAT", Name = "Tỷ lệ mỡ cơ thể", Unit = "%", IsActive = true },
                    new MetricType { Code = "MUSCLE_MASS", Name = "Khối lượng cơ", Unit = "kg", IsActive = true },
                    new MetricType { Code = "WAIST", Name = "Vòng eo", Unit = "cm", IsActive = true }
                );
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Seed dữ liệu mẫu (1-A) vào AppDbContext hoàn tất thành công.");
        }

        // 5. Seed bổ sung (Idempotent) cho phân hệ Lớp học nhóm & Waitlist
        await SeedGroupClassesAndWaitlistDemoAsync();

        // 6. Seed bổ sung (Idempotent) cho phân hệ PT 1-kèm-1, Tiến độ InBody & Đánh giá
        await SeedPtAndProgressDemoAsync();

        // 7. Seed bổ sung (Idempotent) danh mục gói tập đa dạng, gói PT & yêu cầu Freeze chờ duyệt
        await SeedPlansAndFreezeDemoAsync();
    }

    private async Task SeedGroupClassesAndWaitlistDemoAsync()
    {
        var vnOffset = TimeSpan.FromHours(7);
        var nowUtc = DateTime.UtcNow;
        var todayVn = nowUtc.Add(vnOffset).Date;

        var branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
        if (branches.Count == 0) return;

        var firstBranch = branches[0];
        var secondBranch = branches.Count > 1 ? branches[1] : firstBranch;
        var thirdBranch = branches.Count > 2 ? branches[2] : firstBranch;

        // Đảm bảo member2 (minhanh@ooad.vn) có gói Standard Active để demo so sánh độ ưu tiên Waitlist (VIP > Standard)
        var member1 = await _context.Members
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.User.Email == "hoivien@ooad.vn");
        var member2 = await _context.Members
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.User.Email == "minhanh@ooad.vn");

        var standardPlan = await _context.MembershipPlans
            .OrderBy(p => p.PriorityLevel)
            .FirstOrDefaultAsync(p => p.PlanType == "MEMBERSHIP");

        if (member2 != null && standardPlan != null &&
            !await _context.Memberships.AnyAsync(ms => ms.MemberId == member2.Id && ms.Status == MembershipStatus.Active))
        {
            var ms2 = new Membership
            {
                MemberId = member2.Id,
                PlanId = standardPlan.Id,
                BranchId = firstBranch.Id,
                StartDate = DateTime.UtcNow.AddDays(-5),
                EndDate = DateTime.UtcNow.AddDays(25),
                RemainingSessions = standardPlan.PtSessions,
                Status = MembershipStatus.Active
            };
            await _context.Memberships.AddAsync(ms2);
            await _context.SaveChangesAsync();
        }

        // Seed 5 Lớp học nhóm & Buổi học định kỳ + Dữ liệu mẫu điểm danh / đặt chỗ
        if (!await _context.Classes.AnyAsync())
        {
            var trainer = await _context.Trainers.FirstOrDefaultAsync();
            if (trainer == null) return;

            var yogaClass = new GymClass
            {
                BranchId = firstBranch.Id,
                TrainerId = trainer.Id,
                Name = "Yoga Flow Sáng",
                Room = "Phòng Yoga",
                Capacity = 15,
                DayOfWeek = 1, // Thứ 2
                StartTime = new TimeSpan(7, 30, 0),
                EndTime = new TimeSpan(8, 30, 0)
            };

            var hiitClass = new GymClass
            {
                BranchId = firstBranch.Id,
                TrainerId = trainer.Id,
                Name = "HIIT Đốt Mỡ",
                Room = "Studio A",
                Capacity = 2, // Sĩ số nhỏ để dễ test Waitlist
                DayOfWeek = (int)todayVn.AddDays(1).DayOfWeek,
                StartTime = new TimeSpan(18, 30, 0),
                EndTime = new TimeSpan(19, 15, 0)
            };

            var boxingClass = new GymClass
            {
                BranchId = secondBranch.Id,
                TrainerId = trainer.Id,
                Name = "Boxing Cardio",
                Room = "Phòng Boxing",
                Capacity = 16,
                DayOfWeek = 3, // Thứ 4
                StartTime = new TimeSpan(17, 30, 0),
                EndTime = new TimeSpan(18, 30, 0)
            };

            var pilatesClass = new GymClass
            {
                BranchId = firstBranch.Id,
                TrainerId = trainer.Id,
                Name = "Pilates Cốt Lõi",
                Room = "Phòng Yoga",
                Capacity = 12,
                DayOfWeek = 5, // Thứ 6
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(10, 0, 0)
            };

            var cyclingClass = new GymClass
            {
                BranchId = thirdBranch.Id,
                TrainerId = trainer.Id,
                Name = "Cycling Sức Bền",
                Room = "Phòng Cycling",
                Capacity = 20,
                DayOfWeek = 6, // Thứ 7
                StartTime = new TimeSpan(15, 30, 0),
                EndTime = new TimeSpan(16, 30, 0)
            };

            var classes = new List<GymClass> { yogaClass, hiitClass, boxingClass, pilatesClass, cyclingClass };
            await _context.Classes.AddRangeAsync(classes);
            await _context.SaveChangesAsync();

            // Sinh buổi học cho 14 ngày tới theo lịch lặp của từng lớp
            var sessions = new List<ClassSession>();
            foreach (var gc in classes)
            {
                for (int offset = 0; offset <= 14; offset++)
                {
                    var dVn = todayVn.AddDays(offset);
                    if ((int)dVn.DayOfWeek != gc.DayOfWeek) continue;

                    var startUtc = dVn.Add(gc.StartTime).Subtract(vnOffset);
                    var endUtc = dVn.Add(gc.EndTime).Subtract(vnOffset);
                    if (offset == 0 && startUtc <= nowUtc) continue;

                    sessions.Add(new ClassSession
                    {
                        ClassId = gc.Id,
                        TrainerId = gc.TrainerId,
                        SessionDate = dVn,
                        StartAt = startUtc,
                        EndAt = endUtc,
                        Status = "SCHEDULED"
                    });
                }
            }

            // Buổi học hôm nay (đã bắt đầu trước 2h) phục vụ HLV test Điểm danh & đối chiếu Check-in
            var pastStartVn = nowUtc.Add(vnOffset).AddHours(-2);
            var pastSession = new ClassSession
            {
                ClassId = yogaClass.Id,
                TrainerId = yogaClass.TrainerId,
                SessionDate = pastStartVn.Date,
                StartAt = pastStartVn.Subtract(vnOffset),
                EndAt = pastStartVn.AddHours(1).Subtract(vnOffset),
                Status = "SCHEDULED"
            };
            sessions.Add(pastSession);

            // Đảm bảo có 1 buổi HIIT Đốt Mỡ ngày mai lúc 18:30 (cách > 12h để test Waitlist)
            var tomorrowVn = todayVn.AddDays(1);
            var tomorrowHiitStartUtc = tomorrowVn.Add(hiitClass.StartTime).Subtract(vnOffset);
            var tomorrowHiitSession = sessions.FirstOrDefault(s => s.ClassId == hiitClass.Id && s.SessionDate == tomorrowVn);
            if (tomorrowHiitSession == null)
            {
                tomorrowHiitSession = new ClassSession
                {
                    ClassId = hiitClass.Id,
                    TrainerId = hiitClass.TrainerId,
                    SessionDate = tomorrowVn,
                    StartAt = tomorrowHiitStartUtc,
                    EndAt = tomorrowVn.Add(hiitClass.EndTime).Subtract(vnOffset),
                    Status = "SCHEDULED"
                };
                sessions.Add(tomorrowHiitSession);
            }

            await _context.ClassSessions.AddRangeAsync(sessions);
            await _context.SaveChangesAsync();

            // Tạo booking mẫu cho buổi cần điểm danh (member1 đã check-in, member2 chưa check-in)
            if (member1 != null)
            {
                var ms1 = await _context.Memberships
                    .FirstOrDefaultAsync(m => m.MemberId == member1.Id && m.Status == MembershipStatus.Active);

                if (ms1 != null)
                {
                    await _context.Bookings.AddAsync(new Booking
                    {
                        MemberId = member1.Id,
                        MembershipId = ms1.Id,
                        Type = BookingType.Class,
                        ClassSessionId = pastSession.Id,
                        Status = BookingStatus.Confirmed,
                        CreatedAt = nowUtc.AddHours(-12)
                    });

                    var dayFromUtc = pastStartVn.Date.Subtract(vnOffset);
                    var dayToUtc = pastStartVn.Date.AddDays(1).Subtract(vnOffset);
                    var hasCheckinToday = await _context.Checkins.AnyAsync(c =>
                        c.MemberId == member1.Id &&
                        c.BranchId == firstBranch.Id &&
                        c.CheckInAt >= dayFromUtc &&
                        c.CheckInAt < dayToUtc);

                    if (!hasCheckinToday)
                    {
                        await _context.Checkins.AddAsync(new Checkin
                        {
                            MemberId = member1.Id,
                            BranchId = firstBranch.Id,
                            Method = CheckinMethod.QR,
                            CheckInAt = nowUtc.AddHours(-2).AddMinutes(-10),
                            Note = "Check-in mẫu phục vụ kiểm thử điểm danh lớp nhóm"
                        });
                    }
                }
            }

            if (member2 != null)
            {
                var ms2 = await _context.Memberships
                    .FirstOrDefaultAsync(m => m.MemberId == member2.Id && m.Status == MembershipStatus.Active);

                if (ms2 != null)
                {
                    await _context.Bookings.AddAsync(new Booking
                    {
                        MemberId = member2.Id,
                        MembershipId = ms2.Id,
                        Type = BookingType.Class,
                        ClassSessionId = pastSession.Id,
                        Status = BookingStatus.Confirmed,
                        CreatedAt = nowUtc.AddHours(-10)
                    });

                    // Đặt trước 1 chỗ trong buổi HIIT ngày mai (Capacity = 2) để còn 1 chỗ cuối
                    await _context.Bookings.AddAsync(new Booking
                    {
                        MemberId = member2.Id,
                        MembershipId = ms2.Id,
                        Type = BookingType.Class,
                        ClassSessionId = tomorrowHiitSession.Id,
                        Status = BookingStatus.Confirmed,
                        CreatedAt = nowUtc.AddHours(-3)
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Seed dữ liệu mẫu Lớp học nhóm & Buổi học thành công.");
        }
    }

    private async Task SeedPtAndProgressDemoAsync()
    {
        var vnOffset = TimeSpan.FromHours(7);
        var nowUtc = DateTime.UtcNow;
        var todayVn = nowUtc.Add(vnOffset).Date;

        var trainer = await _context.Trainers.Include(t => t.User).Include(t => t.Branch).FirstOrDefaultAsync();
        var member = await _context.Members.Include(m => m.User).FirstOrDefaultAsync(m => m.User.Email == "hoivien@ooad.vn");
        if (trainer == null || member == null) return;

        // 1. Đảm bảo MetricTypes đầy đủ
        var existingTypes = await _context.MetricTypes.ToListAsync();
        var requiredCodes = new[]
        {
            ("WEIGHT", "Cân nặng", "kg"),
            ("HEIGHT", "Chiều cao", "cm"),
            ("BMI", "Chỉ số BMI", "kg/m2"),
            ("BODY_FAT", "Tỷ lệ mỡ cơ thể", "%"),
            ("MUSCLE_MASS", "Khối lượng cơ", "kg"),
            ("WAIST", "Vòng eo", "cm"),
            ("CHEST", "Vòng ngực", "cm"),
            ("HIPS", "Vòng mông", "cm")
        };

        foreach (var (code, name, unit) in requiredCodes)
        {
            if (!existingTypes.Any(t => t.Code == code))
            {
                var mt = new MetricType { Code = code, Name = name, Unit = unit, IsActive = true };
                await _context.MetricTypes.AddAsync(mt);
                existingTypes.Add(mt);
            }
        }
        await _context.SaveChangesAsync();

        // 2. Mở sẵn các khung giờ rảnh (Availability Slots) cho HLV trong 7 ngày tới
        if (!await _context.TrainerAvailabilities.AnyAsync(a => a.TrainerId == trainer.Id && a.StartAt >= nowUtc))
        {
            var slots = new List<TrainerAvailability>();
            var times = new[]
            {
                (new TimeSpan(8, 30, 0), new TimeSpan(9, 30, 0)),
                (new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0)),
                (new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0)),
                (new TimeSpan(15, 30, 0), new TimeSpan(16, 30, 0)),
                (new TimeSpan(19, 0, 0), new TimeSpan(20, 0, 0))
            };

            for (int offset = 0; offset <= 7; offset++)
            {
                var dVn = todayVn.AddDays(offset);
                foreach (var (start, end) in times)
                {
                    var startUtc = dVn.Add(start).Subtract(vnOffset);
                    var endUtc = dVn.Add(end).Subtract(vnOffset);

                    if (startUtc <= nowUtc) continue;

                    // Kiểm tra tránh trùng lịch lớp học
                    var hasConflictClass = await _context.ClassSessions.AnyAsync(s =>
                        s.TrainerId == trainer.Id && s.Status != "CANCELLED" && s.StartAt < endUtc && s.EndAt > startUtc);

                    if (!hasConflictClass)
                    {
                        slots.Add(new TrainerAvailability
                        {
                            TrainerId = trainer.Id,
                            StartAt = startUtc,
                            EndAt = endUtc,
                            IsBooked = false
                        });
                    }
                }
            }

            if (slots.Count > 0)
            {
                await _context.TrainerAvailabilities.AddRangeAsync(slots);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Seed thành công {slots.Count} khung giờ rảnh cho HLV {trainer.User.FullName}.");
            }
        }

        // 3. Seed dữ liệu PT Bookings (Past Attended + Confirmed + Pending)
        var ms = await _context.Memberships.FirstOrDefaultAsync(m => m.MemberId == member.Id && m.Status == MembershipStatus.Active);
        if (ms != null && !await _context.Bookings.AnyAsync(b => b.MemberId == member.Id && b.Type == BookingType.PersonalTrainer))
        {
            // Buổi quá khứ đã hoàn thành (Attended) kèm Đánh giá 5 sao
            var pastAv = new TrainerAvailability
            {
                TrainerId = trainer.Id,
                StartAt = nowUtc.AddDays(-2).AddHours(-4),
                EndAt = nowUtc.AddDays(-2).AddHours(-3),
                IsBooked = true
            };
            await _context.TrainerAvailabilities.AddAsync(pastAv);
            await _context.SaveChangesAsync();

            var pastBooking = new Booking
            {
                MemberId = member.Id,
                MembershipId = ms.Id,
                Type = BookingType.PersonalTrainer,
                AvailabilityId = pastAv.Id,
                Status = BookingStatus.Attended,
                CreatedAt = nowUtc.AddDays(-5)
            };
            await _context.Bookings.AddAsync(pastBooking);
            await _context.SaveChangesAsync();

            // Đánh giá cho buổi học này
            await _context.Reviews.AddAsync(new Review
            {
                MemberId = member.Id,
                TrainerId = trainer.Id,
                BookingId = pastBooking.Id,
                Rating = 5,
                Comment = "HLV hướng dẫn rất chi tiết, kỹ thuật chuẩn và tạo động lực cao! Sau buổi tập thấy cơ bắp kích hoạt rất rõ.",
                CreatedAt = nowUtc.AddDays(-2).AddHours(-2)
            });

            // Đánh giá thứ hai từ học viên khác
            var member2 = await _context.Members.FirstOrDefaultAsync(m => m.User.Email == "minhanh@ooad.vn");
            if (member2 != null)
            {
                await _context.Reviews.AddAsync(new Review
                {
                    MemberId = member2.Id,
                    TrainerId = trainer.Id,
                    Rating = 5,
                    Comment = "Nhiệt tình, giáo án cá nhân hóa rất phù hợp với thể trạng của mình.",
                    CreatedAt = nowUtc.AddDays(-6)
                });
            }

            // Buổi ngày mai đã xác nhận (Confirmed)
            var tomorrowAv = await _context.TrainerAvailabilities
                .FirstOrDefaultAsync(a => a.TrainerId == trainer.Id && a.StartAt > nowUtc.AddHours(12) && !a.IsBooked);

            if (tomorrowAv != null)
            {
                tomorrowAv.IsBooked = true;
                await _context.Bookings.AddAsync(new Booking
                {
                    MemberId = member.Id,
                    MembershipId = ms.Id,
                    Type = BookingType.PersonalTrainer,
                    AvailabilityId = tomorrowAv.Id,
                    Status = BookingStatus.Confirmed,
                    CreatedAt = nowUtc.AddHours(-10)
                });
            }

            // Buổi ngày kia đang chờ xác nhận (Pending)
            var nextAv = await _context.TrainerAvailabilities
                .FirstOrDefaultAsync(a => a.TrainerId == trainer.Id && a.StartAt > nowUtc.AddHours(36) && !a.IsBooked);

            if (nextAv != null)
            {
                nextAv.IsBooked = true;
                await _context.Bookings.AddAsync(new Booking
                {
                    MemberId = member.Id,
                    MembershipId = ms.Id,
                    Type = BookingType.PersonalTrainer,
                    AvailabilityId = nextAv.Id,
                    Status = BookingStatus.Pending,
                    CreatedAt = nowUtc.AddHours(-2)
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Seed dữ liệu mẫu Lịch hẹn PT và Đánh giá HLV thành công.");
        }

        // 4. Seed dữ liệu Tiến độ đo chỉ số cơ thể (Body Measurements)
        if (!await _context.BodyMeasurements.AnyAsync(m => m.MemberId == member.Id))
        {
            var metricWeight = existingTypes.First(t => t.Code == "WEIGHT");
            var metricHeight = existingTypes.First(t => t.Code == "HEIGHT");
            var metricBmi = existingTypes.First(t => t.Code == "BMI");
            var metricFat = existingTypes.First(t => t.Code == "BODY_FAT");
            var metricMuscle = existingTypes.First(t => t.Code == "MUSCLE_MASS");
            var metricWaist = existingTypes.First(t => t.Code == "WAIST");
            var metricChest = existingTypes.First(t => t.Code == "CHEST");
            var metricHips = existingTypes.First(t => t.Code == "HIPS");

            var m1 = new BodyMeasurement
            {
                MemberId = member.Id,
                MeasuredAt = nowUtc.AddDays(-30),
                IsInitial = true,
                Note = "Mốc đo ban đầu khi đăng ký tập luyện tại phòng gym"
            };
            m1.Values.Add(new MeasurementValue { MetricTypeId = metricWeight.Id, Value = 78.5m });
            m1.Values.Add(new MeasurementValue { MetricTypeId = metricHeight.Id, Value = 175m });
            m1.Values.Add(new MeasurementValue { MetricTypeId = metricBmi.Id, Value = 25.6m });
            m1.Values.Add(new MeasurementValue { MetricTypeId = metricFat.Id, Value = 22.5m });
            m1.Values.Add(new MeasurementValue { MetricTypeId = metricMuscle.Id, Value = 31.8m });
            m1.Values.Add(new MeasurementValue { MetricTypeId = metricWaist.Id, Value = 86m });
            m1.Values.Add(new MeasurementValue { MetricTypeId = metricChest.Id, Value = 95m });
            m1.Values.Add(new MeasurementValue { MetricTypeId = metricHips.Id, Value = 98m });

            var m2 = new BodyMeasurement
            {
                MemberId = member.Id,
                MeasuredAt = nowUtc.AddDays(-14),
                IsInitial = false,
                Note = "Kiểm tra giữa kỳ sau 2 tuần siết cơ cùng HLV Minh Khôi"
            };
            m2.Values.Add(new MeasurementValue { MetricTypeId = metricWeight.Id, Value = 76.6m });
            m2.Values.Add(new MeasurementValue { MetricTypeId = metricHeight.Id, Value = 175m });
            m2.Values.Add(new MeasurementValue { MetricTypeId = metricBmi.Id, Value = 25.0m });
            m2.Values.Add(new MeasurementValue { MetricTypeId = metricFat.Id, Value = 21.0m });
            m2.Values.Add(new MeasurementValue { MetricTypeId = metricMuscle.Id, Value = 32.4m });
            m2.Values.Add(new MeasurementValue { MetricTypeId = metricWaist.Id, Value = 83.5m });
            m2.Values.Add(new MeasurementValue { MetricTypeId = metricChest.Id, Value = 96m });
            m2.Values.Add(new MeasurementValue { MetricTypeId = metricHips.Id, Value = 97m });

            var m3 = new BodyMeasurement
            {
                MemberId = member.Id,
                MeasuredAt = nowUtc.AddDays(-1),
                IsInitial = false,
                Note = "Mốc InBody mới nhất: giảm 3.7kg mỡ, tăng khối lượng cơ nạc"
            };
            m3.Values.Add(new MeasurementValue { MetricTypeId = metricWeight.Id, Value = 74.8m });
            m3.Values.Add(new MeasurementValue { MetricTypeId = metricHeight.Id, Value = 175m });
            m3.Values.Add(new MeasurementValue { MetricTypeId = metricBmi.Id, Value = 24.4m });
            m3.Values.Add(new MeasurementValue { MetricTypeId = metricFat.Id, Value = 19.5m });
            m3.Values.Add(new MeasurementValue { MetricTypeId = metricMuscle.Id, Value = 33.1m });
            m3.Values.Add(new MeasurementValue { MetricTypeId = metricWaist.Id, Value = 81m });
            m3.Values.Add(new MeasurementValue { MetricTypeId = metricChest.Id, Value = 97.5m });
            m3.Values.Add(new MeasurementValue { MetricTypeId = metricHips.Id, Value = 96m });

            await _context.BodyMeasurements.AddRangeAsync(m1, m2, m3);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seed 3 mốc đo chỉ số cơ thể InBody thành công.");
        }
    }

    private async Task SeedPlansAndFreezeDemoAsync()
    {
        var vnOffset = TimeSpan.FromHours(7);
        var todayVn = DateTime.UtcNow.Add(vnOffset).Date;

        // 1. Bổ sung gói hội viên Basic 3 Tháng nếu chưa có
        if (!await _context.MembershipPlans.AnyAsync(p => p.Name == "Gói Basic 3 Tháng"))
        {
            await _context.MembershipPlans.AddAsync(new MembershipPlan
            {
                Name = "Gói Basic 3 Tháng",
                DurationDays = 90,
                Price = 2_100_000m,
                PriorityLevel = 2,
                MaxBookingsPerMonth = 24,
                MaxFreezeTimes = 1,
                MaxFreezeDays = 14,
                IsAllBranches = true,
                PlanType = "MEMBERSHIP",
                PtSessions = 1
            });
            await _context.SaveChangesAsync();
        }

        // 2. Bổ sung các gói PT 1-kèm-1 nếu chưa có gói PlanType = "PT"
        if (!await _context.MembershipPlans.AnyAsync(p => p.PlanType == "PT"))
        {
            await _context.MembershipPlans.AddRangeAsync(
                new MembershipPlan
                {
                    Name = "Gói PT 10 Buổi Cá Nhân",
                    DurationDays = 60,
                    Price = 3_500_000m,
                    PriorityLevel = 1,
                    MaxBookingsPerMonth = 0,
                    MaxFreezeTimes = 0,
                    MaxFreezeDays = 0,
                    IsAllBranches = true,
                    PlanType = "PT",
                    PtSessions = 10
                },
                new MembershipPlan
                {
                    Name = "Gói PT 20 Buổi Chuyên Sâu",
                    DurationDays = 120,
                    Price = 6_400_000m,
                    PriorityLevel = 1,
                    MaxBookingsPerMonth = 0,
                    MaxFreezeTimes = 0,
                    MaxFreezeDays = 0,
                    IsAllBranches = true,
                    PlanType = "PT",
                    PtSessions = 20
                }
            );
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seed danh mục gói PT 1-kèm-1 thành công.");
        }

        // 3. Bổ sung 1 yêu cầu Freeze đang chờ duyệt để Lễ tân & Hội viên test
        if (!await _context.FreezeRequests.AnyAsync())
        {
            var vipMembership = await _context.Memberships
                .Include(m => m.Plan)
                .Where(m => m.Plan.MaxFreezeTimes > 0 && m.Status == MembershipStatus.Active && m.EndDate > DateTime.UtcNow.AddDays(15))
                .FirstOrDefaultAsync();

            if (vipMembership != null)
            {
                var fromDate = todayVn.AddDays(5);
                var days = 7;
                await _context.FreezeRequests.AddAsync(new FreezeRequest
                {
                    MembershipId = vipMembership.Id,
                    FromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Utc),
                    ToDate = DateTime.SpecifyKind(fromDate.AddDays(days - 1), DateTimeKind.Utc),
                    Days = days,
                    Reason = "Đi công tác nước ngoài 1 tuần, cần bảo lưu thời hạn gói tập.",
                    Status = FreezeRequestStatus.Pending,
                    CreatedAt = DateTime.UtcNow.AddHours(-3)
                });
                await _context.SaveChangesAsync();
                _logger.LogInformation("Seed yêu cầu Freeze chờ duyệt mẫu thành công.");
            }
        }
    }
}

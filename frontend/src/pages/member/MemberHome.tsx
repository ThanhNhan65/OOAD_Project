import { useMemo, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { AreaChart, Area, ResponsiveContainer, XAxis, YAxis, Tooltip } from 'recharts';
import { CalendarDays, Dumbbell, QrCode, Flame, Timer, CreditCard, ArrowRight, Sparkles, Activity, AlertTriangle } from 'lucide-react';
import { useApp } from '../../store/AppContext';
import type { MStatus } from '../../store/logic';
import { Badge, StatCard, Empty, Progress } from '../../components/ui';
import { mStatusLabel, cbLabel, ptbLabel } from '../../components/labels';
import { fmtDate, fmtTime, diffDays, todayStr, fmtWeekday } from '../../utils';
import { ptApi, type ApiPtBalanceDto, type ApiPtBookingDto } from '../../api/ptApi';
import { bodyMeasurementsApi, type ApiBodyMeasurementDto } from '../../api/bodyMeasurementsApi';
import { membershipPlansApi, type ApiMembershipDto } from '../../api/membershipPlansApi';
import { bookingsApi, type ApiMyScheduleSummaryDto } from '../../api/bookingsApi';
import { checkinsApi, type ApiCheckinDto } from '../../api/checkinsApi';

const EXPIRY_WARNING_DAYS = 7;

function mapApiMembershipStatus(m: ApiMembershipDto): MStatus {
  const s = (m.status || '').toLowerCase();
  if (s === 'cancelled') return 'cancelled';
  if (s === 'frozen') return 'frozen';
  if (s === 'expired') return 'expired';
  if (m.payStatus === 'pending_payment' || (s === 'pending' && m.payStatus !== 'paid')) {
    return m.payStatus === 'paid' ? 'upcoming' : 'pending_payment';
  }
  const today = todayStr();
  if (s === 'active') {
    if (m.endDate.slice(0, 10) < today) return 'expired';
    if (m.startDate.slice(0, 10) > today) return 'upcoming';
    return 'active';
  }
  if (s === 'pending') return 'upcoming';
  return 'expired';
}

export function MemberHome() {
  const { user } = useApp();

  const [memberships, setMemberships] = useState<ApiMembershipDto[]>([]);
  const [apiBalance, setApiBalance] = useState<ApiPtBalanceDto | null>(null);
  const [apiSchedule, setApiSchedule] = useState<ApiMyScheduleSummaryDto | null>(null);
  const [apiPtBookings, setApiPtBookings] = useState<ApiPtBookingDto[]>([]);
  const [apiCheckins, setApiCheckins] = useState<ApiCheckinDto[]>([]);
  const [apiMeasurements, setApiMeasurements] = useState<ApiBodyMeasurementDto[]>([]);

  useEffect(() => {
    let mounted = true;
    Promise.allSettled([
      membershipPlansApi.getMyMemberships(),
      ptApi.getPtBalance(),
      bookingsApi.getMySchedule(),
      ptApi.getMyPtBookings(),
      checkinsApi.getMyHistory(),
      bodyMeasurementsApi.getMyMeasurements(),
    ]).then(([msRes, bRes, schRes, ptRes, ckRes, mRes]) => {
      if (!mounted) return;
      if (msRes.status === 'fulfilled') setMemberships(msRes.value);
      if (bRes.status === 'fulfilled') setApiBalance(bRes.value);
      if (schRes.status === 'fulfilled') setApiSchedule(schRes.value);
      if (ptRes.status === 'fulfilled') setApiPtBookings(ptRes.value);
      if (ckRes.status === 'fulfilled') setApiCheckins(ckRes.value);
      if (mRes.status === 'fulfilled') setApiMeasurements(mRes.value);
    });
    return () => {
      mounted = false;
    };
  }, []);

  const gymMemberships = useMemo(
    () => memberships.filter((m) => (m.planType || 'MEMBERSHIP').toUpperCase() !== 'PT'),
    [memberships],
  );

  const { current, st } = useMemo(() => {
    const activeOrFrozen = gymMemberships.find((m) => {
      const mapped = mapApiMembershipStatus(m);
      return mapped === 'active' || mapped === 'frozen';
    });
    if (activeOrFrozen) {
      return { current: activeOrFrozen, st: mapApiMembershipStatus(activeOrFrozen) };
    }
    return { current: undefined, st: undefined };
  }, [gymMemberships]);

  const startYmd = current ? current.startDate.slice(0, 10) : '';
  const endYmd = current ? current.endDate.slice(0, 10) : '';
  const daysLeft = current ? Math.max(0, diffDays(todayStr(), endYmd)) : 0;
  const total = current ? Math.max(1, diffDays(startYmd, endYmd)) : 1;
  const scopeText = current
    ? current.isAllBranches
      ? 'Tất cả chi nhánh'
      : current.branchName || 'Theo chi nhánh đã đăng ký'
    : '';

  const metrics = useMemo(
    () =>
      [...apiMeasurements]
        .sort((a, b) => a.measuredAt.localeCompare(b.measuredAt))
        .map((m) => ({
          date: m.measuredAt.slice(0, 10),
          weight: Number(m.weight),
        })),
    [apiMeasurements],
  );

  const monthCheckins = useMemo(() => {
    const cutoff = Date.now() - 30 * 86400000;
    return apiCheckins.filter((c) => new Date(c.checkInAt).getTime() > cutoff);
  }, [apiCheckins]);

  const minutes = useMemo(
    () =>
      monthCheckins.reduce((s, c) => {
        if (typeof c.durationMinutes === 'number' && c.durationMinutes >= 0) return s + c.durationMinutes;
        if (c.checkOutAt) {
          return s + Math.max(0, (new Date(c.checkOutAt).getTime() - new Date(c.checkInAt).getTime()) / 60000);
        }
        return s;
      }, 0),
    [monthCheckins],
  );

  const attended = useMemo(() => {
    const classAttended = (apiSchedule?.bookings ?? []).filter(
      (b) => b.type !== 'PersonalTrainer' && b.status.toLowerCase() === 'attended',
    ).length;
    const ptAttended = apiPtBookings.filter((b) =>
      ['attended', 'completed'].includes(b.status.toLowerCase()),
    ).length;
    return classAttended + ptAttended;
  }, [apiSchedule, apiPtBookings]);

  const events = useMemo(() => {
    const now = Date.now();
    const classEvents = (apiSchedule?.bookings ?? [])
      .filter(
        (b) =>
          b.type !== 'PersonalTrainer' &&
          ['booked', 'confirmed'].includes(b.status.toLowerCase()) &&
          new Date(b.startAt).getTime() > now,
      )
      .map((b) => ({
        at: b.startAt,
        title: b.title,
        sub: `${b.room} · ${b.branchName}`,
        badge: cbLabel[b.status.toLowerCase() as keyof typeof cbLabel] || cbLabel.booked,
        color: b.color || '#7c5cff',
      }));

    const waitlistEvents = (apiSchedule?.waitlistEntries ?? [])
      .filter((w) => ['waiting', 'waitlist'].includes(w.status.toLowerCase()) && new Date(w.startAt).getTime() > now)
      .map((w) => ({
        at: w.startAt,
        title: w.className,
        sub: `${w.room} · ${w.branchName} · Hàng chờ #${w.position}`,
        badge: cbLabel.waitlist,
        color: w.color || '#ffb020',
      }));

    const ptEvents = apiPtBookings
      .filter((b) => ['pending', 'confirmed'].includes(b.status.toLowerCase()) && new Date(b.startAt).getTime() > now)
      .map((b) => ({
        at: b.startAt,
        title: `PT 1-kèm-1 · ${b.trainerName}`,
        sub: b.branchName,
        badge: ptbLabel[b.status.toLowerCase() as keyof typeof ptbLabel] || ['Đã đặt', 'blue'],
        color: '#00d1b2',
      }));

    return [...classEvents, ...waitlistEvents, ...ptEvents].sort((a, b) => a.at.localeCompare(b.at));
  }, [apiSchedule, apiPtBookings]);

  const totalBalance = apiBalance ? apiBalance.total : 0;
  const freeBalance = apiBalance ? apiBalance.freeSessions : 0;
  const pkgBalance = apiBalance ? apiBalance.packageSessions : 0;

  return (
    <>
      <div className="card hero-card mb16" style={{ padding: 28 }}>
        <div className="row between wrap" style={{ gap: 20 }}>
          <div>
            <div className="row"><Sparkles size={18} color="#ff8fb0" /><span className="muted">Xin chào,</span></div>
            <h1 style={{ fontSize: 30, marginTop: 4 }}>{user!.fullName} 👋</h1>
            {current && st ? (
              <div className="row wrap mt8">
                <Badge color="pink">{current.planName}</Badge>
                <Badge color={mStatusLabel[st][1]}>{mStatusLabel[st][0]}</Badge>
                <span className="muted small">Hiệu lực đến {fmtDate(endYmd)} · {scopeText}</span>
              </div>
            ) : (
              <p className="muted mt8">Bạn chưa có gói hội viên còn hiệu lực. Chọn gói để bắt đầu tập luyện!</p>
            )}
          </div>
          <div className="row wrap">
            <Link to="/member/card" className="btn"><QrCode size={17} /> Thẻ check-in</Link>
            <Link to="/member/pt" className="btn"><Dumbbell size={17} /> Đặt lịch PT</Link>
            <Link to="/member/classes" className="btn primary"><CalendarDays size={17} /> Đặt lớp ngay</Link>
          </div>
        </div>
        {current && (
          <div className="mt24">
            <div className="row between small mb16" style={{ marginBottom: 8 }}>
              <span className="muted">Thời hạn gói</span>
              <span className="bold">Còn {daysLeft} ngày</span>
            </div>
            <Progress value={Math.max(0, Math.min(100, 100 - (daysLeft / total) * 100))} />
          </div>
        )}
        {current && daysLeft <= EXPIRY_WARNING_DAYS && (
          <div className="row mt16" style={{ color: '#ffc857' }}>
            <AlertTriangle size={16} /> Gói sắp hết hạn – <Link to="/member/memberships" className="bold" style={{ textDecoration: 'underline' }}>gia hạn ngay</Link>
          </div>
        )}
      </div>

      <div className="grid g4 mb16">
        <StatCard
          icon={<Dumbbell size={22} />}
          label="Buổi PT còn lại"
          value={totalBalance}
          color="#00d1b2"
          sub={`${freeBalance} tặng kèm · ${pkgBalance} gói PT`}
        />
        <StatCard icon={<Flame size={22} />} label="Lượt đến phòng (30 ngày)" value={monthCheckins.length} color="#ff5c8a" />
        <StatCard icon={<Timer size={22} />} label="Giờ tập (30 ngày)" value={`${Math.round(minutes / 60)}h`} color="#ffb020" />
        <StatCard icon={<Activity size={22} />} label="Buổi đã tham gia" value={attended} color="#7c5cff" />
      </div>

      <div className="grid g2">
        <div className="card">
          <div className="card-head">
            <h2><CalendarDays size={18} /> Lịch sắp tới</h2>
            <Link to="/member/bookings" className="btn ghost sm">Tất cả <ArrowRight size={14} /></Link>
          </div>
          {events.length === 0 ? <Empty text="Chưa có lịch nào sắp tới" /> : (
            <div className="col">
              {events.slice(0, 5).map((e, i) => (
                <div className="list-item" key={i}>
                  <div className="date-box" style={{ borderColor: e.color + '66', background: e.color + '22' }}>
                    <div className="d">{new Date(e.at).getDate()}</div>
                    <div className="m">Th{new Date(e.at).getMonth() + 1}</div>
                  </div>
                  <div className="grow" style={{ minWidth: 0 }}>
                    <div className="bold">{e.title}</div>
                    <div className="small muted">{fmtWeekday(e.at)} · {fmtTime(e.at)} · {e.sub}</div>
                  </div>
                  <Badge color={e.badge[1]}>{e.badge[0]}</Badge>
                </div>
              ))}
            </div>
          )}
        </div>
        <div className="card">
          <div className="card-head">
            <h2><Activity size={18} /> Cân nặng & Tiến độ</h2>
            <Link to="/member/progress" className="btn ghost sm">Chi tiết <ArrowRight size={14} /></Link>
          </div>
          {metrics.length < 2 ? <Empty text="Cập nhật chỉ số cơ thể để xem biểu đồ tiến độ" /> : (
            <>
              <div className="row" style={{ gap: 24 }}>
                <div><div className="small muted">Hiện tại</div><div style={{ fontSize: 26, fontWeight: 800 }}>{metrics.at(-1)!.weight} kg</div></div>
                <div><div className="small muted">Thay đổi</div><div style={{ fontSize: 26, fontWeight: 800, color: '#5ef0a0' }}>{(metrics.at(-1)!.weight - metrics[0].weight).toFixed(1)} kg</div></div>
              </div>
              <div style={{ height: 180, marginTop: 10 }}>
                <ResponsiveContainer>
                  <AreaChart data={metrics.map((m) => ({ d: fmtDate(m.date).slice(0, 5), w: m.weight }))}>
                    <defs><linearGradient id="gw" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#7c5cff" stopOpacity={0.5} /><stop offset="100%" stopColor="#7c5cff" stopOpacity={0} /></linearGradient></defs>
                    <XAxis dataKey="d" axisLine={false} tickLine={false} />
                    <YAxis domain={['dataMin - 1', 'dataMax + 1']} hide />
                    <Tooltip />
                    <Area type="monotone" dataKey="w" name="Cân nặng" stroke="#a78bfa" strokeWidth={3} fill="url(#gw)" />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            </>
          )}
        </div>
      </div>
      {!current && (
        <div className="card mt16 row between wrap">
          <div className="row"><CreditCard size={22} color="#a78bfa" /><div><h3>Chọn gói tập phù hợp</h3><div className="small muted">Gói tháng, quý, năm hoặc VIP kèm buổi PT miễn phí.</div></div></div>
          <Link to="/member/memberships" className="btn primary">Xem các gói <ArrowRight size={16} /></Link>
        </div>
      )}
    </>
  );
}

export default MemberHome;

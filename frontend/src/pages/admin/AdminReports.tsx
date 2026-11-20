import { useEffect, useState, useCallback } from 'react';
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Legend,
  CartesianGrid, PieChart, Pie, Cell, LineChart, Line, AreaChart, Area,
} from 'recharts';
import {
  DollarSign, Users, UserPlus, UserMinus, TrendingUp, CalendarDays,
  Dumbbell, Building2, Activity, Download,
} from 'lucide-react';
import { Link } from 'react-router-dom';
import { Badge, PageHead, StatCard, Stars, Tabs, Progress } from '../../components/ui';
import { fmtMoney, fmtMoneyShort, fmtDateTime } from '../../utils';
import { reportsApi, type ReportOverviewDto } from '../../api/reportsApi';
import { branchesApi, type BranchApiDto } from '../../api/branchesApi';

const PALETTE = ['#7c5cff', '#ff5c8a', '#00d1b2', '#ffb020', '#3fa9ff', '#a259ff', '#2ed47a'];
const tooltipMoney = (v: unknown) => fmtMoney(Number(v));

function useReportData(branch: string, months = 6) {
  const [data, setData] = useState<ReportOverviewDto | null>(null);
  const [branches, setBranches] = useState<BranchApiDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setErr('');
    try {
      const [overview, brList] = await Promise.all([
        reportsApi.getOverview(branch, months),
        branchesApi.getAll().catch(() => []),
      ]);
      setData(overview);
      setBranches(brList);
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Không thể tải dữ liệu báo cáo');
    } finally {
      setLoading(false);
    }
  }, [branch, months]);

  useEffect(() => {
    load();
  }, [load]);

  return { data, branches, loading, err };
}

function BranchFilter({
  branches,
  value,
  onChange,
}: {
  branches: BranchApiDto[];
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <select className="input" style={{ width: 210 }} value={value} onChange={(e) => onChange(e.target.value)} id="branch-filter">
      <option value="all">Toàn hệ thống</option>
      {branches.map((b) => (
        <option key={b.id} value={b.id}>{b.name}</option>
      ))}
    </select>
  );
}

/* ================= Dashboard admin ================= */
export function AdminHome() {
  const [branch, setBranch] = useState('all');
  const { data, branches, loading, err } = useReportData(branch, 6);

  const rev = data?.revenue ?? [];
  const cur = rev.at(-1) ?? { membership: 0, pt: 0, label: '', key: '', byBranch: {} };
  const prev = rev.at(-2) ?? { membership: 0, pt: 0, label: '', key: '', byBranch: {} };
  const curTotal = cur.membership + cur.pt;
  const prevTotal = prev.membership + prev.pt;
  const growth = prevTotal ? ((curTotal - prevTotal) / prevTotal) * 100 : 0;

  const churn = data?.churn ?? [];
  const curChurn = churn.at(-1) ?? { newMembers: 0, lost: 0, active: 0, rate: 0, label: '' };
  const lastChurn = churn.at(-2) ?? curChurn;

  return (
    <>
      <PageHead
        title="Dashboard quản trị"
        desc="Bức tranh tổng thể hoạt động kinh doanh & vận hành."
        actions={
          <>
            <BranchFilter branches={branches} value={branch} onChange={setBranch} />
            <Link to="/admin/reports" className="btn primary">
              <TrendingUp size={16} /> Báo cáo chi tiết
            </Link>
          </>
        }
      />

      {err && <div className="card mb16" style={{ borderColor: 'var(--red)', color: 'var(--red)', padding: 12 }}>{err}</div>}
      {loading || !data ? (
        <div className="card" style={{ padding: 24 }}>Đang tổng hợp dữ liệu dashboard...</div>
      ) : (
        <>
          <div className="grid g4 mb16">
            <StatCard
              icon={<DollarSign size={22} />}
              label="Doanh thu tháng này"
              value={fmtMoneyShort(curTotal)}
              color="#7c5cff"
              trend={{ v: `${Math.abs(growth).toFixed(0)}%`, up: growth >= 0 }}
              sub={`Tháng trước: ${fmtMoney(prevTotal)}`}
            />
            <StatCard
              icon={<Users size={22} />}
              label="Hội viên đang hoạt động"
              value={data.activeMembersNow}
              color="#00d1b2"
              sub={`Tổng ${data.totalMembers} tài khoản hội viên`}
            />
            <StatCard icon={<UserPlus size={22} />} label="Hội viên mới tháng này" value={curChurn.newMembers} color="#2ed47a" />
            <StatCard
              icon={<UserMinus size={22} />}
              label={`Churn rate ${lastChurn.label}`}
              value={`${lastChurn.rate}%`}
              color="#ff5c8a"
              sub={`${lastChurn.lost}/${lastChurn.active} hội viên rời đi`}
            />
          </div>

          <div className="grid g3 mb16">
            <div className="card span2">
              <div className="card-head">
                <h2><DollarSign size={18} /> Doanh thu 6 tháng</h2>
                <Badge color="purple" plain>Gói hội viên + PT</Badge>
              </div>
              <div style={{ height: 300 }}>
                <ResponsiveContainer>
                  <AreaChart data={data.revenue}>
                    <defs>
                      <linearGradient id="g1" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="#7c5cff" stopOpacity={0.6} />
                        <stop offset="100%" stopColor="#7c5cff" stopOpacity={0} />
                      </linearGradient>
                      <linearGradient id="g2" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="#00d1b2" stopOpacity={0.6} />
                        <stop offset="100%" stopColor="#00d1b2" stopOpacity={0} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} />
                    <XAxis dataKey="label" axisLine={false} tickLine={false} />
                    <YAxis tickFormatter={fmtMoneyShort} axisLine={false} tickLine={false} width={60} />
                    <Tooltip formatter={tooltipMoney} />
                    <Legend />
                    <Area type="monotone" dataKey="membership" name="Gói hội viên" stackId="1" stroke="#a78bfa" fill="url(#g1)" strokeWidth={2} />
                    <Area type="monotone" dataKey="pt" name="Gói PT" stackId="1" stroke="#00d1b2" fill="url(#g2)" strokeWidth={2} />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            </div>

            <div className="card">
              <div className="card-head">
                <h2><Activity size={18} /> Đang có mặt</h2>
                <span className="live-dot" />
              </div>
              <div className="col">
                {data.branchPresences.map((b) => (
                  <div key={b.branchId} className="list-item">
                    <Building2 size={20} color="#a78bfa" />
                    <div className="grow">
                      <b>{b.branchName}</b>
                      <div className="small dim">{b.operatingHours}</div>
                    </div>
                    <div style={{ fontSize: 22, fontWeight: 800 }}>{b.presentCount}</div>
                  </div>
                ))}
              </div>
              <div className="divider" />
              <h3 className="mb16">Cơ cấu gói đang hoạt động</h3>
              <div style={{ height: 150 }}>
                <ResponsiveContainer>
                  <PieChart>
                    <Pie data={data.planUsage} dataKey="value" nameKey="name" innerRadius={42} outerRadius={68} paddingAngle={3}>
                      {data.planUsage.map((p) => <Cell key={p.name} fill={p.color} stroke="none" />)}
                    </Pie>
                    <Tooltip />
                  </PieChart>
                </ResponsiveContainer>
              </div>
              <div className="chips" style={{ justifyContent: 'center' }}>
                {data.planUsage.map((p) => (
                  <span key={p.name} className="small row" style={{ gap: 4 }}>
                    <span style={{ width: 8, height: 8, borderRadius: 2, background: p.color }} />
                    {p.name} ({p.value})
                  </span>
                ))}
              </div>
            </div>
          </div>

          <div className="grid g2">
            <div className="card">
              <div className="card-head"><h2><CalendarDays size={18} /> Top lớp học (tỷ lệ lấp đầy)</h2></div>
              <div className="col">
                {[...data.classes].sort((a, b) => b.fillRate - a.fillRate).slice(0, 5).map((x, idx) => (
                  <div key={x.classId}>
                    <div className="row between small">
                      <span><b>{x.className}</b> <span className="dim">· {x.branchName}</span></span>
                      <span className="bold">{x.fillRate}%</span>
                    </div>
                    <div className="mt8" style={{ marginTop: 4 }}>
                      <Progress value={x.fillRate} color={PALETTE[idx % PALETTE.length]} />
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <div className="card">
              <div className="card-head"><h2><DollarSign size={18} /> Giao dịch gần đây</h2></div>
              <div className="col">
                {data.recentPayments.map((p) => (
                  <div className="list-item" key={p.id}>
                    <div className="grow" style={{ minWidth: 0 }}>
                      <b>{p.memberName}</b>
                      <div className="small dim">{p.description} · {fmtDateTime(p.createdAt)}</div>
                    </div>
                    <b style={{ color: '#5ef0a0' }}>+{fmtMoneyShort(p.amount)}</b>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </>
      )}
    </>
  );
}

/* ================= Báo cáo chi tiết ================= */
export function AdminReports() {
  const [branch, setBranch] = useState('all');
  const [months, setMonths] = useState(6);
  const [tab, setTab] = useState<'revenue' | 'members' | 'classes' | 'trainers' | 'usage'>('revenue');
  const { data, branches, loading, err } = useReportData(branch, months);

  const revenueChartData = (data?.revenue ?? []).map((r) => ({
    ...r,
    ...r.byBranch,
  }));

  const totalRev = (data?.revenue ?? []).reduce((a, r) => a + r.membership + r.pt, 0);

  const exportCsv = () => {
    if (!data) return;
    const rows = [['Tháng', 'Gói hội viên', 'Gói PT', 'Tổng', 'HV đầu kỳ', 'HV mới', 'HV rời đi', 'Churn %']].concat(
      data.revenue.map((r, i) => [
        r.label,
        String(r.membership),
        String(r.pt),
        String(r.membership + r.pt),
        String(data.churn[i]?.active ?? 0),
        String(data.churn[i]?.newMembers ?? 0),
        String(data.churn[i]?.lost ?? 0),
        String(data.churn[i]?.rate ?? 0),
      ]),
    );
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob(['\ufeff' + rows.map((r) => r.join(',')).join('\n')], { type: 'text/csv' }));
    a.download = `bao-cao-${branch}.csv`;
    a.click();
  };

  return (
    <>
      <PageHead
        title="Báo cáo & thống kê"
        desc="Doanh thu, biến động hội viên, hiệu suất lớp học và huấn luyện viên theo chi nhánh."
        actions={
          <>
            <BranchFilter branches={branches} value={branch} onChange={setBranch} />
            <select className="input" style={{ width: 140 }} value={months} onChange={(e) => setMonths(+e.target.value)}>
              <option value={3}>3 tháng</option>
              <option value={6}>6 tháng</option>
              <option value={12}>12 tháng</option>
            </select>
            <button className="btn" onClick={exportCsv} disabled={!data}>
              <Download size={16} /> Xuất CSV
            </button>
          </>
        }
      />

      {err && <div className="card mb16" style={{ borderColor: 'var(--red)', color: 'var(--red)', padding: 12 }}>{err}</div>}

      <Tabs
        value={tab}
        onChange={setTab}
        tabs={[
          { id: 'revenue', label: <><DollarSign size={15} /> Doanh thu</> },
          { id: 'members', label: <><Users size={15} /> Hội viên & churn</> },
          { id: 'usage', label: <><Activity size={15} /> Sử dụng gói & mật độ</> },
          { id: 'classes', label: <><CalendarDays size={15} /> Lớp học</> },
          { id: 'trainers', label: <><Dumbbell size={15} /> Huấn luyện viên</> },
        ]}
      />

      {loading || !data ? (
        <div className="card" style={{ padding: 24 }}>Đang tải báo cáo thống kê...</div>
      ) : (
        <>
          {tab === 'revenue' && (
            <>
              <div className="grid g3 mb16">
                <StatCard icon={<DollarSign size={22} />} label={`Tổng doanh thu ${months} tháng`} value={fmtMoneyShort(totalRev)} color="#7c5cff" />
                <StatCard icon={<Users size={22} />} label="Từ gói hội viên" value={fmtMoneyShort(data.revenue.reduce((a, r) => a + r.membership, 0))} color="#ff5c8a" />
                <StatCard icon={<Dumbbell size={22} />} label="Từ gói PT" value={fmtMoneyShort(data.revenue.reduce((a, r) => a + r.pt, 0))} color="#00d1b2" />
              </div>
              <div className="grid g2">
                <div className="card">
                  <div className="card-head"><h2>Doanh thu theo loại gói</h2></div>
                  <div style={{ height: 300 }}>
                    <ResponsiveContainer>
                      <BarChart data={data.revenue}>
                        <CartesianGrid strokeDasharray="3 3" vertical={false} />
                        <XAxis dataKey="label" axisLine={false} tickLine={false} />
                        <YAxis tickFormatter={fmtMoneyShort} axisLine={false} tickLine={false} width={55} />
                        <Tooltip formatter={tooltipMoney} cursor={{ fill: 'rgba(255,255,255,.04)' }} />
                        <Legend />
                        <Bar dataKey="membership" name="Gói hội viên" stackId="a" fill="#7c5cff" />
                        <Bar dataKey="pt" name="Gói PT" stackId="a" fill="#00d1b2" radius={[6, 6, 0, 0]} />
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </div>
                <div className="card">
                  <div className="card-head"><h2>Doanh thu theo chi nhánh</h2></div>
                  <div style={{ height: 300 }}>
                    <ResponsiveContainer>
                      <LineChart data={revenueChartData}>
                        <CartesianGrid strokeDasharray="3 3" vertical={false} />
                        <XAxis dataKey="label" axisLine={false} tickLine={false} />
                        <YAxis tickFormatter={fmtMoneyShort} axisLine={false} tickLine={false} width={55} />
                        <Tooltip formatter={tooltipMoney} />
                        <Legend />
                        {branches.map((b, i) => (
                          <Line key={b.id} type="monotone" dataKey={b.id} name={b.name} stroke={PALETTE[i % PALETTE.length]} strokeWidth={3} dot={{ r: 4 }} />
                        ))}
                      </LineChart>
                    </ResponsiveContainer>
                  </div>
                </div>
              </div>
              <div className="table-wrap mt16">
                <table>
                  <thead>
                    <tr>
                      <th>Kỳ</th>
                      <th>Gói hội viên</th>
                      <th>Gói PT</th>
                      {branches.map((b) => <th key={b.id}>{b.name}</th>)}
                      <th>Tổng</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.revenue.map((r) => (
                      <tr key={r.key}>
                        <td><b>{r.label}</b></td>
                        <td>{fmtMoney(r.membership)}</td>
                        <td>{fmtMoney(r.pt)}</td>
                        {branches.map((b) => <td key={b.id}>{fmtMoney(r.byBranch[b.id] ?? 0)}</td>)}
                        <td className="bold">{fmtMoney(r.membership + r.pt)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}

          {tab === 'members' && (
            <>
              <div className="grid g2">
                <div className="card">
                  <div className="card-head"><h2>Hội viên mới vs rời đi</h2></div>
                  <div style={{ height: 300 }}>
                    <ResponsiveContainer>
                      <BarChart data={data.churn}>
                        <CartesianGrid strokeDasharray="3 3" vertical={false} />
                        <XAxis dataKey="label" axisLine={false} tickLine={false} />
                        <YAxis allowDecimals={false} axisLine={false} tickLine={false} width={30} />
                        <Tooltip cursor={{ fill: 'rgba(255,255,255,.04)' }} />
                        <Legend />
                        <Bar dataKey="newMembers" name="Mới" fill="#2ed47a" radius={[6, 6, 0, 0]} />
                        <Bar dataKey="lost" name="Rời đi" fill="#ff5c8a" radius={[6, 6, 0, 0]} />
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </div>
                <div className="card">
                  <div className="card-head"><h2>Churn rate (%) & hội viên đầu kỳ</h2></div>
                  <div style={{ height: 300 }}>
                    <ResponsiveContainer>
                      <LineChart data={data.churn}>
                        <CartesianGrid strokeDasharray="3 3" vertical={false} />
                        <XAxis dataKey="label" axisLine={false} tickLine={false} />
                        <YAxis yAxisId="l" axisLine={false} tickLine={false} width={30} />
                        <YAxis yAxisId="r" orientation="right" axisLine={false} tickLine={false} width={30} />
                        <Tooltip />
                        <Legend />
                        <Line yAxisId="l" type="monotone" dataKey="rate" name="Churn %" stroke="#ff5c8a" strokeWidth={3} />
                        <Line yAxisId="r" type="monotone" dataKey="active" name="HV đầu kỳ" stroke="#3fa9ff" strokeWidth={3} />
                      </LineChart>
                    </ResponsiveContainer>
                  </div>
                </div>
              </div>
              <div className="table-wrap mt16">
                <table>
                  <thead><tr><th>Kỳ</th><th>HV hoạt động đầu kỳ</th><th>HV mới</th><th>HV rời đi</th><th>Tăng/giảm ròng</th><th>Churn rate</th></tr></thead>
                  <tbody>
                    {data.churn.map((c) => (
                      <tr key={c.key}>
                        <td><b>{c.label}</b></td>
                        <td>{c.active}</td>
                        <td style={{ color: '#5ef0a0' }}>+{c.newMembers}</td>
                        <td style={{ color: '#ff8aa0' }}>-{c.lost}</td>
                        <td className="bold">{c.newMembers - c.lost >= 0 ? '+' : ''}{c.newMembers - c.lost}</td>
                        <td><Badge color={c.rate > 15 ? 'red' : c.rate > 8 ? 'amber' : 'green'}>{c.rate}%</Badge></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}

          {tab === 'usage' && (
            <>
              <div className="grid g3 mb16">
                <StatCard icon={<Users size={22} />} label="Gói đang hoạt động" value={data.planUsage.reduce((a, p) => a + p.value, 0)} color="#7c5cff" />
                <StatCard icon={<Activity size={22} />} label="Lượt check-in tháng này" value={data.visits.at(-1)?.count ?? 0} color="#ffb020" />
                <StatCard icon={<Building2 size={22} />} label="Đang tập hiện tại" value={data.branchPresences.reduce((a, b) => a + b.presentCount, 0)} color="#00d1b2" />
              </div>
              <div className="grid g2">
                <div className="card">
                  <div className="card-head"><h2>Mật độ check-in theo giờ</h2></div>
                  <div style={{ height: 280 }}>
                    <ResponsiveContainer>
                      <BarChart data={data.hours}>
                        <CartesianGrid strokeDasharray="3 3" vertical={false} />
                        <XAxis dataKey="hour" axisLine={false} tickLine={false} />
                        <YAxis allowDecimals={false} axisLine={false} tickLine={false} width={30} />
                        <Tooltip cursor={{ fill: 'rgba(255,255,255,.04)' }} />
                        <Bar dataKey="count" name="Lượt check-in" fill="#ffb020" radius={[6, 6, 0, 0]} />
                      </BarChart>
                    </ResponsiveContainer>
                  </div>
                </div>
                <div className="card">
                  <div className="card-head"><h2>Phân bố gói hội viên đang hoạt động</h2></div>
                  <div style={{ height: 280 }}>
                    <ResponsiveContainer>
                      <PieChart>
                        <Pie
                          data={data.planUsage}
                          dataKey="value"
                          nameKey="name"
                          outerRadius={100}
                          innerRadius={55}
                          paddingAngle={3}
                          label={(e: { name?: string; value?: number }) => `${e.name}: ${e.value}`}
                        >
                          {data.planUsage.map((p) => <Cell key={p.name} fill={p.color} stroke="none" />)}
                        </Pie>
                        <Tooltip />
                      </PieChart>
                    </ResponsiveContainer>
                  </div>
                </div>
              </div>
            </>
          )}

          {tab === 'classes' && (
            <>
              <div className="card mb16">
                <div className="card-head"><h2>Tỷ lệ lấp đầy & tham gia</h2></div>
                <div style={{ height: 300 }}>
                  <ResponsiveContainer>
                    <BarChart data={data.classes.map((x) => ({ name: x.className, fill: x.fillRate, attendance: x.attendanceRate }))}>
                      <CartesianGrid strokeDasharray="3 3" vertical={false} />
                      <XAxis dataKey="name" axisLine={false} tickLine={false} />
                      <YAxis unit="%" axisLine={false} tickLine={false} width={40} />
                      <Tooltip cursor={{ fill: 'rgba(255,255,255,.04)' }} />
                      <Legend />
                      <Bar dataKey="fill" name="Lấp đầy %" fill="#7c5cff" radius={[6, 6, 0, 0]} />
                      <Bar dataKey="attendance" name="Tham gia %" fill="#00d1b2" radius={[6, 6, 0, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              </div>
              <div className="table-wrap">
                <table>
                  <thead><tr><th>Lớp</th><th>Chi nhánh</th><th>HLV</th><th>Buổi đã dạy</th><th>Lấp đầy</th><th>Tham gia</th><th>Lượt chờ</th><th>Đánh giá</th></tr></thead>
                  <tbody>
                    {data.classes.map((x, idx) => (
                      <tr key={x.classId}>
                        <td><b>{x.className}</b></td>
                        <td className="small">{x.branchName}</td>
                        <td className="small">{x.trainerName}</td>
                        <td>{x.sessions}</td>
                        <td style={{ minWidth: 120 }}>
                          <div className="row small">
                            <div className="grow"><Progress value={x.fillRate} color={PALETTE[idx % PALETTE.length]} /></div>
                            {x.fillRate}%
                          </div>
                        </td>
                        <td>{x.attendanceRate}%</td>
                        <td>{x.waitlisted}</td>
                        <td><div className="row small"><Stars value={x.rating} size={12} />{x.rating ? x.rating.toFixed(1) : '—'} ({x.reviewCount})</div></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}

          {tab === 'trainers' && (
            <>
              <div className="card mb16">
                <div className="card-head"><h2>Số buổi dạy theo HLV</h2></div>
                <div style={{ height: 280 }}>
                  <ResponsiveContainer>
                    <BarChart data={data.trainers.map((x) => ({ name: x.fullName.split(' ').slice(-2).join(' '), pt: x.ptCompleted, cls: x.classSessions }))}>
                      <CartesianGrid strokeDasharray="3 3" vertical={false} />
                      <XAxis dataKey="name" axisLine={false} tickLine={false} />
                      <YAxis allowDecimals={false} axisLine={false} tickLine={false} width={30} />
                      <Tooltip cursor={{ fill: 'rgba(255,255,255,.04)' }} />
                      <Legend />
                      <Bar dataKey="pt" name="Buổi PT hoàn thành" fill="#00d1b2" radius={[6, 6, 0, 0]} />
                      <Bar dataKey="cls" name="Buổi lớp nhóm" fill="#a259ff" radius={[6, 6, 0, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              </div>
              <div className="table-wrap">
                <table>
                  <thead><tr><th>Huấn luyện viên</th><th>PT hoàn thành</th><th>PT vắng</th><th>Từ chối/Hủy</th><th>Buổi lớp nhóm</th><th>Đánh giá</th></tr></thead>
                  <tbody>
                    {[...data.trainers].sort((a, b) => b.rating - a.rating).map((x) => (
                      <tr key={x.trainerId}>
                        <td><b>{x.fullName}</b></td>
                        <td>{x.ptCompleted}</td>
                        <td>{x.ptNoShow}</td>
                        <td>{x.ptRejected}</td>
                        <td>{x.classSessions}</td>
                        <td><div className="row small"><Stars value={x.rating} size={12} /><b>{x.rating ? x.rating.toFixed(2) : '—'}</b> ({x.reviewCount})</div></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </>
      )}
    </>
  );
}
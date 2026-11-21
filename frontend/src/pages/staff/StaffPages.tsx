import { useState, useEffect, useCallback } from 'react';
import { equipmentsApi } from '../../api/equipmentsApi';
import { request } from '../../api/apiClient';
import { Link } from 'react-router-dom';
import { ScanLine, LogIn, LogOut, CheckCircle2, XCircle, Users, Snowflake, Wrench, History, Search, Keyboard, Check, X, Plus, Building2, Download, Zap, ArrowLeft } from 'lucide-react';
import { useApp } from '../../store/AppContext';
import * as L from '../../store/logic';
import { Avatar, Badge, Empty, Modal, PageHead, StatCard, Tabs, ReasonModal } from '../../components/ui';
import { reqLabel, reportLabel, eqLabel, mStatusLabel } from '../../components/labels';
import { fmtDate, fmtDateTime, fmtTime, todayStr, uid as genId } from '../../utils';
import type { CheckIn, ReportStatus, EquipmentStatus, Equipment, EquipmentReport } from '../../types';

/* ================= Dashboard lễ tân ================= */
export function StaffHome() {
  const { db, user } = useApp();
  const br = user!.branchId!;
  const today = todayStr();
  const todayCI = db.checkins.filter((c) => c.branchId === br && c.checkInAt.slice(0, 10) <= today && new Date(c.checkInAt).toLocaleDateString('sv-SE') === today);
  const present = db.checkins.filter((c) => c.branchId === br && c.result === 'success' && !c.checkOutAt);
  const freezes = db.freezeRequests.filter((f) => f.status === 'pending');
  const reports = db.equipmentReports.filter((r) => r.branchId === br && ['pending', 'in_progress'].includes(r.status));
  const sessionsToday = db.sessions.filter((s) => s.date === today && L.classById(db, s.classId)?.branchId === br).sort((a, b) => a.startAt.localeCompare(b.startAt));
  return (
    <>
      <PageHead title={`Quầy lễ tân · ${L.branchById(db, br)?.name}`} desc="Tổng quan vận hành trong ngày." actions={<Link to="/staff/checkin" className="btn primary"><ScanLine size={16} /> Check-in / out</Link>} />
      <div className="grid g4 mb16">
        <StatCard icon={<Users size={22} />} label="Đang có mặt" value={<span className="row"><span className="live-dot" /> {present.length}</span>} color="#2ed47a" />
        <StatCard icon={<LogIn size={22} />} label="Lượt check-in hôm nay" value={todayCI.filter((c) => c.result === 'success').length} color="#7c5cff" sub={`${todayCI.filter((c) => c.result === 'denied').length} lượt bị từ chối`} />
        <StatCard icon={<Snowflake size={22} />} label="Yêu cầu freeze chờ duyệt" value={freezes.length} color="#3fa9ff" />
        <StatCard icon={<Wrench size={22} />} label="Sự cố thiết bị đang mở" value={reports.length} color="#ff5c8a" />
      </div>
      <div className="grid g2">
        <div className="card">
          <div className="card-head"><h2><Users size={18} /> Hội viên đang tập</h2></div>
          {present.length === 0 ? <Empty text="Chưa có ai trong phòng tập" /> : <div className="col">{present.map((c) => { const m = L.userById(db, c.memberId); return <div className="list-item" key={c.id}><Avatar user={m} size="sm" /><div className="grow"><b>{m?.fullName}</b><div className="small dim">{m?.cardCode} · vào lúc {fmtTime(c.checkInAt)}</div></div>{c.method === 'manual' && <Badge color="amber" plain>Thủ công</Badge>}</div>; })}</div>}
        </div>
        <div className="card">
          <div className="card-head"><h2>Lớp học hôm nay</h2></div>
          {sessionsToday.length === 0 ? <Empty text="Không có lớp hôm nay" /> : <div className="col">{sessionsToday.map((s) => { const c = L.classById(db, s.classId)!; return <div className="list-item" key={s.id}><div className="date-box" style={{ background: c.color + '22', borderColor: c.color + '66' }}><div className="d" style={{ fontSize: 15 }}>{fmtTime(s.startAt)}</div></div><div className="grow"><b>{c.name}</b><div className="small dim">{s.room} · HLV {L.userById(db, s.trainerId)?.fullName}</div></div><Badge color={s.status === 'cancelled' ? 'red' : 'gray'}>{s.status === 'cancelled' ? 'Đã hủy' : `${L.bookedCount(db, s.id)}/${c.capacity}`}</Badge></div>; })}</div>}
        </div>
      </div>
    </>
  );
}

/* ================= Máy quét & thủ công ================= */
type Result = { ok: boolean; title: string; record?: CheckIn; memberId?: string; msg?: string };

function ResultPanel({ r }: { r: Result }) {
  const { db } = useApp();
  const m = L.userById(db, r.memberId);
  const ms = m ? L.membershipOn(db, m.id) : undefined;
  const plan = ms ? L.planById(db, ms.planId) : undefined;
  return (
    <div className={`result-panel ${r.ok ? 'ok' : 'no'}`}>
      <div className="row">{r.ok ? <CheckCircle2 size={34} color="#2ed47a" /> : <XCircle size={34} color="#ff4d6d" />}<div><h2>{r.title}</h2>{r.msg && <div className="muted">{r.msg}</div>}</div></div>
      {m && (
        <div className="row mt16">
          <Avatar user={m} />
          <div className="grow"><b>{m.fullName}</b><div className="small muted">{m.cardCode} · {plan ? `${plan.name} · HSD ${fmtDate(ms!.endDate)}` : 'Không có gói hiệu lực'}</div></div>
          {ms && <Badge color={mStatusLabel[L.membershipStatus(db, ms)][1]}>{mStatusLabel[L.membershipStatus(db, ms)][0]}</Badge>}
        </div>
      )}
    </div>
  );
}

function Scanner({ branchId, staffId }: { branchId: string; staffId?: string }) {
  const { db, act } = useApp();
  const [code, setCode] = useState('');
  const [res, setRes] = useState<Result | null>(null);
  const members = db.users.filter((u) => u.role === 'member').slice(0, 10);
  const run = (mode: 'in' | 'out', c = code) => {
    if (!c.trim()) return;
    if (mode === 'in') {
      const r = act((d) => L.checkIn(d, { code: c, branchId, method: 'card' }));
      if (r) setRes(r.record.result === 'success' ? { ok: true, title: 'Check-in thành công', memberId: r.member.id, msg: 'Chào mừng bạn đến tập!', record: r.record } : { ok: false, title: 'Từ chối check-in', memberId: r.member.id, msg: r.record.denyReason });
      else setRes({ ok: false, title: 'Không nhận diện được thẻ' });
    } else {
      const r = act((d) => L.checkOut(d, { code: c, method: 'card', staffId }));
      if (r) {
        const mins = Math.round((new Date(r.record.checkOutAt!).getTime() - new Date(r.record.checkInAt).getTime()) / 60000);
        setRes({ ok: true, title: 'Check-out thành công', memberId: r.member.id, msg: `Thời lượng tập: ${Math.floor(mins / 60)}h${String(mins % 60).padStart(2, '0')}p. Hẹn gặp lại!` });
      }
    }
    setCode('');
  };
  return (
    <div className="grid g2">
      <div>
        <div className="scanner">
          {[[0, 0], [1, 0], [0, 1], [1, 1]].map(([x, y], i) => (
            <span key={i} className="corner" style={{ [x ? 'right' : 'left']: 24, [y ? 'bottom' : 'top']: 24, borderRight: x ? undefined : 'none', borderLeft: x ? 'none' : undefined, borderBottom: y ? undefined : 'none', borderTop: y ? 'none' : undefined, borderRadius: 6 } as React.CSSProperties} />
          ))}
          <div className="line" />
          <div style={{ textAlign: 'center' }}><ScanLine size={46} color="#00d1b2" /><div className="muted mt8">Đưa thẻ / mã QR vào vùng quét</div></div>
        </div>
        <div className="row mt16">
          <input className="input grow" placeholder="Mã thẻ (VD: DF100001)" value={code} onChange={(e) => setCode(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && run('in')} id="scan-code" />
          <button className="btn success" onClick={() => run('in')} id="scan-in"><LogIn size={16} /> Vào</button>
          <button className="btn" onClick={() => run('out')} id="scan-out"><LogOut size={16} /> Ra</button>
        </div>
        <div className="small dim mt16 mb16" style={{ marginBottom: 6 }}>Mô phỏng quẹt thẻ nhanh:</div>
        <div className="chips">{members.map((m) => <button key={m.id} className="chip" onClick={() => run(L.openCheckin(db, m.id) ? 'out' : 'in', m.cardCode!)} title={m.fullName}>{m.cardCode} · {m.fullName.split(' ').at(-1)}</button>)}</div>
      </div>
      <div>{res ? <ResultPanel r={res} /> : <div className="card" style={{ height: '100%' }}><Empty text="Kết quả quét sẽ hiển thị tại đây" icon={<ScanLine size={40} />} /></div>}</div>
    </div>
  );
}

export function StaffCheckin() {
  const { db, user, act } = useApp();
  const br = user!.branchId!;
  const [tab, setTab] = useState<'scan' | 'manual'>('scan');
  const [q, setQ] = useState('');
  const [reason, setReason] = useState('Quên thẻ');
  const [res, setRes] = useState<Result | null>(null);
  const found = q.trim().length >= 2 ? db.users.filter((u) => u.role === 'member' && (u.fullName.toLowerCase().includes(q.toLowerCase()) || u.phone.includes(q) || u.cardCode?.toLowerCase().includes(q.toLowerCase()) || u.email.includes(q.toLowerCase()))).slice(0, 8) : [];
  const manualIn = (mid: string) => {
    const r = act((d) => L.checkIn(d, { code: mid, branchId: br, method: 'manual', staffId: user!.id, reason }));
    if (r) setRes(r.record.result === 'success' ? { ok: true, title: 'Đã check-in thủ công', memberId: mid, msg: `Lý do: ${reason}` } : { ok: false, title: 'Từ chối check-in', memberId: mid, msg: r.record.denyReason });
  };
  const manualOut = (mid: string) => {
    const r = act((d) => L.checkOut(d, { code: mid, method: 'manual', staffId: user!.id }));
    if (r) setRes({ ok: true, title: 'Đã check-out thủ công', memberId: mid });
  };
  const present = db.checkins.filter((c) => c.branchId === br && c.result === 'success' && !c.checkOutAt);
  return (
    <>
      <PageHead title="Check-in / Check-out" desc={<>Chi nhánh <b>{L.branchById(db, br)?.name}</b> · <span className="live-dot" /> {present.length} người đang có mặt · Tự động check-out lúc {L.branchById(db, br)?.closeTime}</>} />
      <Tabs value={tab} onChange={setTab} tabs={[{ id: 'scan', label: <><ScanLine size={15} /> Quét thẻ / QR</> }, { id: 'manual', label: <><Keyboard size={15} /> Nhập thủ công</> }]} />
      {tab === 'scan' ? <Scanner branchId={br} staffId={user!.id} /> : (
        <div className="grid g2">
          <div className="card">
            <div className="field"><label>Tìm hội viên (tên, SĐT, email, mã thẻ)</label><div className="input-icon"><Search size={17} /><input className="input" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Nhập ít nhất 2 ký tự" id="manual-search" /></div></div>
            <div className="field mt16"><label>Lý do nhập thủ công</label><div className="chips">{['Quên thẻ', 'Thẻ bị hỏng', 'Máy quét lỗi', 'Khác'].map((r) => <button key={r} className={`chip ${reason === r ? 'on' : ''}`} onClick={() => setReason(r)}>{r}</button>)}</div></div>
            <div className="col mt16">
              {found.map((m) => {
                const open = L.openCheckin(db, m.id);
                return (
                  <div className="list-item" key={m.id}>
                    <Avatar user={m} size="sm" />
                    <div className="grow"><b>{m.fullName}</b><div className="small dim">{m.cardCode} · {m.phone}</div></div>
                    {open ? <button className="btn sm" onClick={() => manualOut(m.id)}><LogOut size={14} /> Check-out</button> : <button className="btn sm success" onClick={() => manualIn(m.id)} id={`manual-in-${m.id}`}><LogIn size={14} /> Check-in</button>}
                  </div>
                );
              })}
              {q.length >= 2 && found.length === 0 && <Empty text="Không tìm thấy hội viên" />}
            </div>
          </div>
          <div>{res ? <ResultPanel r={res} /> : <div className="card"><Empty text="Hệ thống áp dụng cùng điều kiện như quét thẻ và ghi nhận nhân viên thực hiện + lý do." /></div>}</div>
        </div>
      )}
    </>
  );
}

/* ================= Kiosk (public) ================= */
export function Kiosk() {
  const { db } = useApp();
  const [br, setBr] = useState(db.branches[0].id);
  return (
    <div style={{ maxWidth: 1100, margin: '0 auto', padding: 32 }}>
      <div className="row between mb16">
        <div className="brand" style={{ padding: 0 }}><div className="brand-logo"><Zap size={22} color="#fff" fill="#fff" /></div><div><div className="brand-name">OOAD<span className="grad-text">Center</span></div><div className="brand-sub">Kiosk check-in</div></div></div>
        <div className="row"><select className="input" value={br} onChange={(e) => setBr(e.target.value)} id="kiosk-branch">{db.branches.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}</select><Link to="/login" className="btn"><ArrowLeft size={16} /> Thoát</Link></div>
      </div>
      <div className="card mb16 row"><span className="live-dot" /> <b>{L.presentCount(db, br)}</b> người đang tập tại {L.branchById(db, br)?.name} · Giờ mở cửa {L.branchById(db, br)?.openTime}–{L.branchById(db, br)?.closeTime}</div>
      <Scanner branchId={br} />
    </div>
  );
}

/* ================= Lịch sử check-in (lễ tân & admin) ================= */
export function CheckinHistory() {
  const { db, user } = useApp();
  const isAdmin = user!.role === 'admin';
  const [br, setBr] = useState(isAdmin ? 'all' : user!.branchId!);
  const [res, setRes] = useState<'all' | 'success' | 'denied'>('all');
  const [method, setMethod] = useState<'all' | 'card' | 'manual'>('all');
  const [date, setDate] = useState('');
  const [q, setQ] = useState('');
  const list = db.checkins.filter((c) => (br === 'all' || c.branchId === br) && (res === 'all' || c.result === res) && (method === 'all' || c.method === method) && (!date || new Date(c.checkInAt).toLocaleDateString('sv-SE') === date) && (!q || L.userById(db, c.memberId)?.fullName.toLowerCase().includes(q.toLowerCase()) || L.userById(db, c.memberId)?.cardCode?.toLowerCase().includes(q.toLowerCase())));
  const exportCsv = () => {
    const rows = [['Hội viên', 'Mã thẻ', 'Chi nhánh', 'Vào', 'Ra', 'Hình thức', 'Kết quả', 'Lý do', 'Nhân viên']].concat(
      list.map((c) => [L.userById(db, c.memberId)?.fullName ?? '', L.userById(db, c.memberId)?.cardCode ?? '', L.branchById(db, c.branchId)?.name ?? '', fmtDateTime(c.checkInAt), fmtDateTime(c.checkOutAt), c.method, c.result, c.denyReason ?? c.reason ?? '', L.userById(db, c.staffId)?.fullName ?? '']),
    );
    const blob = new Blob(['\ufeff' + rows.map((r) => r.map((x) => `"${x}"`).join(',')).join('\n')], { type: 'text/csv' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'lich-su-checkin.csv';
    a.click();
  };
  return (
    <>
      <PageHead title="Lịch sử check-in" desc={`${list.length} lượt · ${list.filter((c) => c.result === 'denied').length} bị từ chối · ${list.filter((c) => c.method === 'manual').length} nhập thủ công`} actions={<button className="btn" onClick={exportCsv}><Download size={16} /> Xuất CSV</button>} />
      <div className="row wrap mb16">
        {isAdmin && <select className="input" style={{ width: 200 }} value={br} onChange={(e) => setBr(e.target.value)}><option value="all">Tất cả chi nhánh</option>{db.branches.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}</select>}
        <select className="input" style={{ width: 160 }} value={res} onChange={(e) => setRes(e.target.value as typeof res)}><option value="all">Mọi kết quả</option><option value="success">Hợp lệ</option><option value="denied">Bị từ chối</option></select>
        <select className="input" style={{ width: 160 }} value={method} onChange={(e) => setMethod(e.target.value as typeof method)}><option value="all">Mọi hình thức</option><option value="card">Quét thẻ</option><option value="manual">Thủ công</option></select>
        <input type="date" className="input" style={{ width: 170 }} value={date} onChange={(e) => setDate(e.target.value)} />
        <div className="input-icon grow" style={{ minWidth: 200 }}><Search size={17} /><input className="input" placeholder="Tìm hội viên..." value={q} onChange={(e) => setQ(e.target.value)} /></div>
      </div>
      <div className="table-wrap">
        <table>
          <thead><tr><th>Hội viên</th><th>Chi nhánh</th><th>Vào</th><th>Ra</th><th>Hình thức</th><th>Kết quả</th><th>Ghi chú</th></tr></thead>
          <tbody>
            {list.slice(0, 200).map((c) => {
              const m = L.userById(db, c.memberId);
              return (
                <tr key={c.id}>
                  <td><div className="row"><Avatar user={m} size="sm" /><div><b>{m?.fullName}</b><div className="small dim">{m?.cardCode}</div></div></div></td>
                  <td className="small">{L.branchById(db, c.branchId)?.name}</td>
                  <td className="small">{fmtDateTime(c.checkInAt)}</td>
                  <td className="small">{c.checkOutAt ? <>{fmtTime(c.checkOutAt)} {c.checkoutMethod === 'auto' && <Badge color="gray" plain>Tự động</Badge>}{c.checkoutMethod === 'manual' && <Badge color="amber" plain>Thủ công</Badge>}</> : c.result === 'success' ? <Badge color="green">Đang tập</Badge> : '—'}</td>
                  <td>{c.method === 'card' ? <Badge color="blue" plain>Quét thẻ</Badge> : <Badge color="amber" plain>Thủ công</Badge>}</td>
                  <td><Badge color={c.result === 'success' ? 'green' : 'red'}>{c.result === 'success' ? 'Hợp lệ' : 'Từ chối'}</Badge></td>
                  <td className="small muted">{c.denyReason ?? (c.method === 'manual' ? `${c.reason} · NV ${L.userById(db, c.staffId)?.fullName}` : '')}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
        {list.length === 0 && <Empty text="Không có dữ liệu" icon={<History size={40} />} />}
      </div>
    </>
  );
}

/* ================= Freeze ================= */
export function StaffFreeze() {
  const { db, user, act } = useApp();
  const [tab, setTab] = useState<'pending' | 'done'>('pending');
  const [reject, setReject] = useState<string | null>(null);
  const list = db.freezeRequests.filter((f) => (tab === 'pending' ? f.status === 'pending' : f.status !== 'pending'));
  return (
    <>
      <PageHead title="Yêu cầu tạm ngưng (freeze)" desc="Kiểm tra điều kiện gói và xử lý yêu cầu của hội viên." />
      <Tabs value={tab} onChange={setTab} tabs={[{ id: 'pending', label: 'Chờ duyệt', count: db.freezeRequests.filter((f) => f.status === 'pending').length }, { id: 'done', label: 'Đã xử lý' }]} />
      {list.length === 0 ? <div className="card"><Empty text="Không có yêu cầu" icon={<Snowflake size={40} />} /></div> : (
        <div className="grid g2">
          {list.map((f) => {
            const m = L.userById(db, f.memberId);
            const ms = db.memberships.find((x) => x.id === f.membershipId)!;
            const p = L.planById(db, ms.planId)!;
            const affected = db.classBookings.filter((b) => b.memberId === f.memberId && ['booked', 'waitlist'].includes(b.status) && (() => { const s = L.sessionById(db, b.sessionId)!; return s.date >= f.startDate && s.date <= f.endDate; })()).length + db.ptBookings.filter((b) => b.memberId === f.memberId && ['pending', 'confirmed'].includes(b.status) && new Date(b.startAt).toLocaleDateString('sv-SE') >= f.startDate && new Date(b.startAt).toLocaleDateString('sv-SE') <= f.endDate).length;
            return (
              <div className="card" key={f.id}>
                <div className="row"><Avatar user={m} /><div className="grow"><b>{m?.fullName}</b><div className="small dim">{m?.phone} · gửi {fmtDateTime(f.createdAt)}</div></div><Badge color={reqLabel[f.status][1]}>{reqLabel[f.status][0]}</Badge></div>
                <div className="mt16">
                  <div className="kv"><span>Gói</span><span>{p.name} (HSD {fmtDate(ms.endDate)})</span></div>
                  <div className="kv"><span>Khoảng tạm ngưng</span><span>{fmtDate(f.startDate)} → {fmtDate(f.endDate)} ({f.days} ngày)</span></div>
                  <div className="kv"><span>Đã dùng</span><span>{ms.freezeCountUsed}/{p.maxFreezeTimes} lần · {ms.freezeDaysUsed}/{p.maxFreezeDays} ngày</span></div>
                  {f.status === 'pending' && <div className="kv"><span>Lịch bị ảnh hưởng</span><span style={{ color: affected ? '#ffc857' : undefined }}>{affected} lịch sẽ tự hủy</span></div>}
                  <div className="kv"><span>Lý do</span><span>{f.reason}</span></div>
                  {f.status !== 'pending' && <div className="kv"><span>Xử lý bởi</span><span>{L.userById(db, f.handledBy)?.fullName} · {fmtDateTime(f.handledAt)}{f.note ? ` · ${f.note}` : ''}</span></div>}
                </div>
                {f.status === 'pending' && (
                  <div className="row mt16" style={{ justifyContent: 'flex-end' }}>
                    <button className="btn danger sm" onClick={() => setReject(f.id)}><X size={14} /> Từ chối</button>
                    <button className="btn success sm" onClick={() => act((d) => L.handleFreeze(d, f.id, user!.id, true, ''), 'Đã duyệt – hạn gói được cộng thêm ' + f.days + ' ngày')} id={`approve-${f.id}`}><Check size={14} /> Duyệt</button>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
      {reject && <ReasonModal title="Từ chối yêu cầu freeze" label="Lý do" confirmText="Từ chối" danger required onClose={() => setReject(null)} onConfirm={(r) => { act((d) => L.handleFreeze(d, reject, user!.id, false, r), 'Đã từ chối yêu cầu'); setReject(null); }} />}
    </>
  );
}

/* ================= Thiết bị (lễ tân & admin) ================= */
/* ================= Thiết bị (lễ tân & admin) ================= */
type BadgeColor = Parameters<typeof Badge>[0]['color'];

const getEqInfo = (status: string): [string, BadgeColor] =>
  (eqLabel as Record<string, [string, BadgeColor]>)[status] ?? [status, 'gray'];

const getReportInfo = (status: string): [string, BadgeColor] =>
  (reportLabel as Record<string, [string, BadgeColor]>)[status] ?? [status, 'gray'];

const ALL_EQ_STATUSES: EquipmentStatus[] = ['active', 'broken', 'repairing', 'retired'];

export function EquipmentManage() {
  const { db, user } = useApp();
  const isAdmin = user!.role === 'admin';
  const [tab, setTab] = useState<'reports' | 'list' | 'summary'>(isAdmin ? 'summary' : 'reports');
  const [br, setBr] = useState(isAdmin ? 'all' : user!.branchId ?? 'all');
  const [branches, setBranches] = useState<{ id: string; name: string }[]>([]);
  const [allEqs, setAllEqs] = useState<Equipment[]>([]);
  const [allReports, setAllReports] = useState<EquipmentReport[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState('');
  const [upd, setUpd] = useState<EquipmentReport | null>(null);
  const [add, setAdd] = useState(false);
  const [rec, setRec] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<{
    id: string;
    name: string;
    code?: string;
    from: EquipmentStatus;
    to: EquipmentStatus;
  } | null>(null);
  const [updatingStatus, setUpdatingStatus] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    setErr('');
    try {
      const [brList, eqList, repList] = await Promise.all([
        request<{ id: string; name: string }[]>('/branches').catch(() => db.branches),
        equipmentsApi.getAll(),
        equipmentsApi.getReports(),
      ]);
      setBranches(brList);
      setAllEqs(eqList);
      setAllReports(repList);
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Không thể tải dữ liệu thiết bị');
    } finally {
      setLoading(false);
    }
  }, [db.branches]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const validBr = br === 'all' || branches.some((b) => b.id === br) ? br : 'all';
  const inBr = (b?: string) => validBr === 'all' || !b || b === validBr;
  const reports = allReports.filter((r) => inBr(r.branchId ?? allEqs.find((e) => e.id === r.equipmentId)?.branchId));
  const eqs = allEqs.filter((e) => inBr(e.branchId));

  const branchName = (id?: string) =>
    branches.find((b) => b.id === id)?.name ?? L.branchById(db, id ?? '')?.name ?? '—';

  const confirmChangeStatus = async () => {
    if (!pendingStatus) return;
    setUpdatingStatus(true);
    setErr('');
    try {
      await equipmentsApi.updateStatus(pendingStatus.id, pendingStatus.to);
      setPendingStatus(null);
      await loadData();
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Cập nhật trạng thái thất bại');
    } finally {
      setUpdatingStatus(false);
    }
  };

  return (
    <>
      <PageHead
        title="Thiết bị & sự cố"
        desc={isAdmin ? 'Báo cáo tình trạng thiết bị toàn hệ thống.' : 'Tiếp nhận phản ánh, ghi nhận thiết bị hỏng và cập nhật sau sửa chữa.'}
        actions={<>
          {isAdmin && (
            <select className="input" style={{ width: 200 }} value={br} onChange={(e) => setBr(e.target.value)}>
              <option value="all">Tất cả chi nhánh</option>
              {branches.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}
            </select>
          )}
          {!isAdmin && <button className="btn" onClick={() => setRec(true)} id="record-broken"><Wrench size={16} /> Ghi nhận thiết bị hỏng</button>}
          {isAdmin && <button className="btn primary" onClick={() => setAdd(true)} id="add-equipment"><Plus size={16} /> Thêm thiết bị</button>}
        </>}
      />

      {err && <div className="card mb16" style={{ borderColor: 'var(--red)', color: 'var(--red)', padding: 12 }}>{err}</div>}

      <Tabs
        value={tab}
        onChange={setTab}
        tabs={[
          ...(isAdmin ? [{ id: 'summary' as const, label: 'Tổng quan' }] : []),
          { id: 'reports', label: 'Phản ánh / sự cố', count: reports.filter((r) => r.status === 'pending').length },
          { id: 'list', label: 'Danh sách thiết bị' },
        ]}
      />

      {loading ? (
        <div className="card" style={{ padding: 24 }}>Đang tải dữ liệu thiết bị...</div>
      ) : (
        <>
          {tab === 'summary' && (
            <>
              <div className="grid g4 mb16">
                {ALL_EQ_STATUSES.map((s) => (
                  <StatCard
                    key={s}
                    icon={<Wrench size={22} />}
                    label={getEqInfo(s)[0]}
                    value={eqs.filter((e) => e.status === s).length}
                    color={{ active: '#2ed47a', broken: '#ff4d6d', repairing: '#ffb020', retired: '#6d7290' }[s]}
                  />
                ))}
              </div>
              <div className="table-wrap">
                <table>
                  <thead><tr><th>Chi nhánh</th><th>Tổng</th><th>Hoạt động</th><th>Hỏng</th><th>Đang sửa</th><th>Thanh lý</th><th>Sự cố mở</th><th>Tỷ lệ sẵn sàng</th></tr></thead>
                  <tbody>
                    {branches.map((b) => {
                      const l = allEqs.filter((e) => e.branchId === b.id);
                      const act2 = l.filter((e) => e.status === 'active').length;
                      const usable = l.filter((e) => e.status !== 'retired').length;
                      const openReps = allReports.filter(
                        (r) => (r.branchId ?? allEqs.find((e) => e.id === r.equipmentId)?.branchId) === b.id && ['pending', 'in_progress'].includes(r.status),
                      ).length;
                      return (
                        <tr key={b.id}>
                          <td><b>{b.name}</b></td>
                          <td>{l.length}</td>
                          <td>{act2}</td>
                          <td style={{ color: '#ff8aa0' }}>{l.filter((e) => e.status === 'broken').length}</td>
                          <td style={{ color: '#ffc857' }}>{l.filter((e) => e.status === 'repairing').length}</td>
                          <td>{l.filter((e) => e.status === 'retired').length}</td>
                          <td>{openReps}</td>
                          <td><b>{usable ? Math.round((act2 / usable) * 100) : 0}%</b></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </>
          )}

          {tab === 'reports' && (reports.length === 0 ? <div className="card"><Empty text="Không có phản ánh" /></div> : (
            <div className="table-wrap">
              <table>
                <thead><tr><th>Thiết bị</th><th>Chi nhánh</th><th>Người báo</th><th>Mô tả</th><th>Thời gian</th><th>Trạng thái</th><th></th></tr></thead>
                <tbody>
                  {reports.map((r) => {
                    const e = allEqs.find((x) => x.id === r.equipmentId);
                    const [repText, repColor] = getReportInfo(r.status);
                    const lastNote = r.updates?.at(-1)?.note;
                    return (
                      <tr key={r.id}>
                        <td><b>{e?.name ?? 'Thiết bị'}</b><div className="small dim">{e?.code}</div></td>
                        <td className="small">{branchName(r.branchId ?? e?.branchId)}</td>
                        <td className="small">{L.userById(db, r.reporterId)?.fullName ?? r.reporterId ?? '—'}</td>
                        <td className="small muted" style={{ maxWidth: 260 }}>
                          {r.description}
                          {lastNote && lastNote !== r.description && <div className="dim">{lastNote}</div>}
                        </td>
                        <td className="small">{fmtDateTime(r.createdAt)}</td>
                        <td><Badge color={repColor}>{repText}</Badge></td>
                        <td>
                          {!isAdmin && r.status !== 'resolved' && r.status !== 'rejected' && (
                            <button className="btn sm" onClick={() => setUpd(r)} id={`update-${r.id}`}>Cập nhật</button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ))}

          {tab === 'list' && (
            <div className="table-wrap">
              <table>
                <thead><tr><th>Mã</th><th>Thiết bị</th><th>Loại</th><th>Chi nhánh</th><th>Ngày nhập</th><th>Tình trạng</th>{isAdmin && <th>Đổi trạng thái</th>}</tr></thead>
                <tbody>
                  {eqs.map((e) => {
                    const [eqText, eqColor] = getEqInfo(e.status);
                    return (
                      <tr key={e.id}>
                        <td className="small" style={{ fontFamily: 'monospace' }}>{e.code}</td>
                        <td><b>{e.name}</b></td>
                        <td className="small">{e.category}</td>
                        <td className="small">{branchName(e.branchId)}</td>
                        <td className="small">{fmtDate(e.addedAt)}</td>
                        <td><Badge color={eqColor}>{eqText}</Badge></td>
                        {isAdmin && (
                          <td>
                            {e.status === 'retired' ? (
                              <span className="small dim">Đã khóa</span>
                            ) : (
                              <select
                                className="input"
                                style={{ width: 150, padding: '6px 10px' }}
                                value={e.status}
                                onChange={(ev) => {
                                  const next = ev.target.value as EquipmentStatus;
                                  if (next !== e.status) {
                                    setPendingStatus({
                                      id: e.id,
                                      name: e.name,
                                      code: e.code,
                                      from: e.status,
                                      to: next,
                                    });
                                  }
                                }}
                              >
                                {ALL_EQ_STATUSES.map((s) => (
                                  <option key={s} value={s}>{getEqInfo(s)[0]}</option>
                                ))}
                              </select>
                            )}
                          </td>
                        )}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {pendingStatus && (
        <Modal
          title="Xác nhận đổi trạng thái thiết bị"
          onClose={() => !updatingStatus && setPendingStatus(null)}
          footer={
            <>
              <button className="btn" disabled={updatingStatus} onClick={() => setPendingStatus(null)}>Hủy</button>
              <button
                className={`btn ${pendingStatus.to === 'retired' ? 'danger' : 'primary'}`}
                disabled={updatingStatus}
                onClick={confirmChangeStatus}
                id="confirm-change-eq-status"
              >
                {updatingStatus ? 'Đang lưu...' : 'Đồng ý đổi'}
              </button>
            </>
          }
        >
          <p className="mb12">
            Bạn có chắc chắn muốn đổi trạng thái thiết bị <b>{pendingStatus.name}</b>
            {pendingStatus.code ? ` (${pendingStatus.code})` : ''}?
          </p>
          <div className="row" style={{ gap: 8, alignItems: 'center' }}>
            <Badge color={getEqInfo(pendingStatus.from)[1]}>{getEqInfo(pendingStatus.from)[0]}</Badge>
            <span>→</span>
            <Badge color={getEqInfo(pendingStatus.to)[1]}>{getEqInfo(pendingStatus.to)[0]}</Badge>
          </div>
          {pendingStatus.to === 'retired' && (
            <p className="small mt12" style={{ color: 'var(--red)' }}>
              ⚠️ Lưu ý: Sau khi chuyển sang <b>Thanh lý</b>, thiết bị sẽ bị khóa và không thể đổi lại trạng thái khác.
            </p>
          )}
        </Modal>
      )}

      {upd && <UpdateReportModal report={upd} onClose={() => setUpd(null)} onSaved={loadData} />}
      {add && <AddEquipmentModal branches={branches} existingCount={(bid) => allEqs.filter((e) => e.branchId === bid).length} onClose={() => setAdd(false)} onSaved={loadData} />}
      {rec && <RecordBrokenModal equipments={eqs.filter((e) => e.status === 'active')} onClose={() => setRec(false)} onSaved={loadData} />}
    </>
  );
}

function UpdateReportModal({ report, onClose, onSaved }: { report: EquipmentReport; onClose: () => void; onSaved: () => Promise<void> }) {
  const [status, setStatus] = useState<ReportStatus>(report.status === 'pending' ? 'in_progress' : 'resolved');
  const [note, setNote] = useState('');
  const [saving, setSaving] = useState(false);
  const [err, setErr] = useState('');

  const save = async () => {
    setSaving(true);
    setErr('');
    try {
      await equipmentsApi.updateReportStatus(report.id, status, note.trim());
      await onSaved();
      onClose();
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Cập nhật thất bại');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      title="Cập nhật xử lý sự cố"
      onClose={onClose}
      footer={<>
        <button className="btn" onClick={onClose}>Hủy</button>
        <button className="btn primary" disabled={saving} onClick={save} id="report-save">{saving ? 'Đang lưu...' : 'Lưu'}</button>
      </>}
    >
      {err && <div className="small mb12" style={{ color: 'var(--red)' }}>{err}</div>}
      <div className="field">
        <label>Trạng thái</label>
        <div className="chips">
          {(['in_progress', 'resolved', 'rejected'] as ReportStatus[]).map((s) => (
            <button key={s} className={`chip ${status === s ? 'on' : ''}`} onClick={() => setStatus(s)}>{getReportInfo(s)[0]}</button>
          ))}
        </div>
      </div>
      <div className="field mt16">
        <label>Ghi chú quá trình xử lý</label>
        <textarea className="input" value={note} onChange={(e) => setNote(e.target.value)} placeholder="VD: Đã thay dây curoa..." />
      </div>
    </Modal>
  );
}

function RecordBrokenModal({ equipments, onClose, onSaved }: { equipments: Equipment[]; onClose: () => void; onSaved: () => Promise<void> }) {
  const [eq, setEq] = useState('');
  const [desc, setDesc] = useState('');
  const [saving, setSaving] = useState(false);
  const [err, setErr] = useState('');

  const save = async () => {
    setSaving(true);
    setErr('');
    try {
      await equipmentsApi.createReport({ equipmentId: eq, description: desc.trim() });
      await onSaved();
      onClose();
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Ghi nhận thất bại');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      title="Ghi nhận thiết bị hỏng"
      onClose={onClose}
      footer={<>
        <button className="btn" onClick={onClose}>Hủy</button>
        <button className="btn primary" disabled={!eq || !desc.trim() || saving} onClick={save}>{saving ? 'Đang lưu...' : 'Ghi nhận'}</button>
      </>}
    >
      {err && <div className="small mb12" style={{ color: 'var(--red)' }}>{err}</div>}
      <div className="field">
        <label>Thiết bị</label>
        <select className="input" value={eq} onChange={(e) => setEq(e.target.value)}>
          <option value="">— Chọn —</option>
          {equipments.map((e) => <option key={e.id} value={e.id}>{e.code ? `${e.code} · ` : ''}{e.name}</option>)}
        </select>
      </div>
      <div className="field mt16">
        <label>Tình trạng</label>
        <textarea className="input" value={desc} onChange={(e) => setDesc(e.target.value)} />
      </div>
    </Modal>
  );
}

function AddEquipmentModal({
  branches,
  existingCount,
  onClose,
  onSaved,
}: {
  branches: { id: string; name: string }[];
  existingCount: (branchId: string) => number;
  onClose: () => void;
  onSaved: () => Promise<void>;
}) {
  const [f, setF] = useState({ name: '', category: 'Cardio', branchId: branches[0]?.id ?? '', qty: 1 });
  const [saving, setSaving] = useState(false);
  const [err, setErr] = useState('');

  const save = async () => {
    if (!f.name.trim()) return setErr('Nhập tên thiết bị');
    if (!f.branchId) return setErr('Chọn chi nhánh');

    setSaving(true);
    setErr('');
    try {
      const base = existingCount(f.branchId);
      const prefix = f.branchId.slice(0, 6).toUpperCase();
      for (let i = 0; i < f.qty; i++) {
        await equipmentsApi.create({
          branchId: f.branchId,
          name: f.name.trim(),
          category: f.category,
          code: `${prefix}-${String(base + i + 1).padStart(3, '0')}`,
          status: 'active',
        });
      }
      await onSaved();
      onClose();
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Thêm thiết bị thất bại');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      title="Thêm thiết bị mới"
      onClose={onClose}
      footer={<>
        <button className="btn" onClick={onClose}>Hủy</button>
        <button className="btn primary" disabled={saving} onClick={save} id="equipment-save">{saving ? 'Đang thêm...' : 'Thêm'}</button>
      </>}
    >
      {err && <div className="small mb12" style={{ color: 'var(--red)' }}>{err}</div>}
      <div className="form-grid">
        <div className="field full"><label>Tên thiết bị</label><input className="input" value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} id="equipment-name" /></div>
        <div className="field"><label>Loại</label><select className="input" value={f.category} onChange={(e) => setF({ ...f, category: e.target.value })}>{['Cardio', 'Sức mạnh', 'Tạ tự do', 'Phụ kiện'].map((c) => <option key={c}>{c}</option>)}</select></div>
        <div className="field"><label>Số lượng</label><input type="number" min={1} className="input" value={f.qty} onChange={(e) => setF({ ...f, qty: Math.max(1, +e.target.value) })} /></div>
        <div className="field full"><label>Chi nhánh</label><select className="input" value={f.branchId} onChange={(e) => setF({ ...f, branchId: e.target.value })}>{branches.map((b) => <option key={b.id} value={b.id}>{b.name}</option>)}</select></div>
      </div>
    </Modal>
  );
}

/* ================= Tra cứu hội viên ================= */
export function StaffMembers() {
  const { db } = useApp();
  const [q, setQ] = useState('');
  const term = q.trim().toLowerCase();
  const list = db.users.filter(
    (u) =>
      u.role === 'member' &&
      (!term ||
        u.fullName.toLowerCase().includes(term) ||
        u.email.toLowerCase().includes(term) ||
        u.phone.includes(term) ||
        u.cardCode?.toLowerCase().includes(term)),
  );
  return (
    <>
      <PageHead
        title="Tra cứu hội viên"
        desc="Kiểm tra nhanh hiệu lực gói và trạng thái."
        actions={<Link to="/staff/member-cards" className="btn primary">Quản lý Cấp / Khóa Thẻ & QR</Link>}
      />
      <div className="input-icon mb16" style={{ maxWidth: 420 }}><Search size={17} /><input className="input" placeholder="Tìm theo tên, SĐT, email, mã thẻ..." value={q} onChange={(e) => setQ(e.target.value)} /></div>
      <div className="table-wrap">
        <table>
          <thead><tr><th>Hội viên</th><th>Mã thẻ & Trạng thái</th><th>SĐT / Email</th><th>Gói hiện tại</th><th>Hiệu lực</th><th>Buổi PT</th><th>Trạng thái gói</th></tr></thead>
          <tbody>{list.map((u) => { const ms = L.membershipOn(db, u.id); const latest = L.latestPaidMembership(db, u.id); const st = ms ? L.membershipStatus(db, ms) : latest ? L.membershipStatus(db, latest) : null; const bal = L.ptBalance(db, u.id); const cardSt = L.activeCardOf(db, u.id)?.status ?? u.cardStatus ?? 'ACTIVE'; return (
            <tr key={u.id}><td><div className="row"><Avatar user={u} size="sm" /><div><b>{u.fullName}</b>{u.locked && <Badge color="red">Khóa</Badge>}</div></div></td><td><div className="row" style={{ gap: 6 }}><span style={{ fontFamily: 'monospace', fontWeight: 700 }}>{u.cardCode}</span><Badge color={cardSt === 'ACTIVE' ? 'green' : cardSt === 'LOCKED' ? 'amber' : 'red'}>{cardSt}</Badge></div></td><td className="small"><div>{u.phone}</div><div className="dim">{u.email}</div></td><td>{ms ? L.planById(db, ms.planId)?.name : '—'}</td><td className="small">{latest ? `${fmtDate(latest.startDate)} → ${fmtDate(latest.endDate)}` : '—'}</td><td>{bal.total}</td><td>{st ? <Badge color={mStatusLabel[st][1]}>{mStatusLabel[st][0]}</Badge> : <Badge>Chưa mua</Badge>}</td></tr>); })}</tbody>
        </table>
      </div>
    </>
  );
}
export { Building2 };
export { StaffMemberCardsScreen as StaffMemberCards } from './screens/StaffMemberCardsScreen';

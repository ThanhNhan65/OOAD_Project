import { useState, useMemo, useEffect, useCallback } from 'react';
import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, Legend, CartesianGrid } from 'recharts';
import {
  Save, Plus, Scale, Ruler, Percent, Dumbbell, Activity, Receipt, Wrench, QrCode, MapPin, Clock, Trash2,
  Sparkles, Eye, History,
} from 'lucide-react';
import { useApp } from '../../store/AppContext';
import * as L from '../../store/logic';
import { Avatar, Badge, Empty, Modal, PageHead, StatCard, Tabs } from '../../components/ui';
import { formatPaymentMethod, reportLabel, eqLabel } from '../../components/labels';
import { MemberCardWidget } from '../../components/member-card';
import { fmtDate, fmtDateTime, fmtMoney, todayStr, calcBMI, bmiLabel, uid as genId, fmtTime } from '../../utils';
import { bodyMeasurementsApi, type ApiBodyMeasurementDto } from '../../api/bodyMeasurementsApi';
import { equipmentsApi } from '../../api/equipmentsApi';
import { emitAppDataChanged } from '../../api/notificationsApi';
import { membershipPlansApi, type ApiMembershipDto, type ApiPaymentDto } from '../../api/membershipPlansApi';
import { checkinsApi, type ApiCheckinDto } from '../../api/checkinsApi';
import { userService } from '../../api/user.service';
import { authService } from '../../api/auth.service';
import { getAuthToken, isApiNetworkError, request } from '../../api/apiClient';
import type { User, BodyMetric, Equipment, EquipmentReport } from '../../types';

/* ================= Hồ sơ ================= */
export function MemberProfile() {
  const { user, updateCurrentUser, toast } = useApp();
  const [f, setF] = useState<User>(user!);
  const [pw, setPw] = useState({ old: '', n1: '', n2: '' });
  const set = (k: keyof User) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setF({ ...f, [k]: e.target.value });

  useEffect(() => {
    let mounted = true;
    userService
      .getMe()
      .then((me) => {
        if (!mounted) return;
        setF((prev) => ({
          ...prev,
          fullName: me.fullName || prev.fullName,
          phone: me.phone ?? prev.phone,
          address: me.address ?? prev.address,
          dob: me.dateOfBirth ? me.dateOfBirth.slice(0, 10) : prev.dob,
          gender: (me.gender as User['gender']) || prev.gender,
          cardCode: me.cardCode ?? prev.cardCode,
        }));
      })
      .catch(() => {});
    return () => {
      mounted = false;
    };
  }, []);

  const save = async () => {
    if (!f.fullName.trim()) {
      toast('Họ tên không được để trống', 'error');
      return;
    }

    try {
      const updated = await userService.updateMe({
        fullName: f.fullName.trim(),
        phone: f.phone.trim(),
        address: f.address,
        dateOfBirth: f.dob || undefined,
        gender: f.gender,
      });
      updateCurrentUser({
        fullName: updated.fullName || f.fullName.trim(),
        phone: updated.phone ?? f.phone.trim(),
        address: updated.address ?? f.address,
        dob: updated.dateOfBirth ? updated.dateOfBirth.slice(0, 10) : f.dob,
        gender: (updated.gender as User['gender']) || f.gender,
      });
      toast('Đã lưu hồ sơ', 'success');
    } catch (err) {
      toast((err as Error).message || 'Không thể lưu hồ sơ', 'error');
    }
  };

  const changePw = async () => {
    if (!pw.old) return toast('Vui lòng nhập mật khẩu hiện tại', 'error');
    if (pw.n1.length < 6) return toast('Mật khẩu mới tối thiểu 6 ký tự', 'error');
    if (pw.n1 !== pw.n2) return toast('Xác nhận mật khẩu không khớp', 'error');

    try {
      await authService.changePassword({
        currentPassword: pw.old,
        newPassword: pw.n1,
        confirmPassword: pw.n2,
      });
      setPw({ old: '', n1: '', n2: '' });
      toast('Đổi mật khẩu thành công', 'success');
    } catch (err) {
      toast((err as Error).message || 'Không thể đổi mật khẩu', 'error');
    }
  };
  return (
    <>
      <PageHead title="Hồ sơ cá nhân" desc="Cập nhật thông tin liên hệ và bảo mật tài khoản." />
      <div className="grid g3">
        <div className="card" style={{ textAlign: 'center' }}>
          <div style={{ display: 'flex', justifyContent: 'center' }}><Avatar user={f} size="lg" /></div>
          <h2 className="mt16">{f.fullName}</h2>
          <div className="small muted">{f.email}</div>
          {user!.cardCode && <div className="mt16"><Badge color="purple">Mã thẻ: {user!.cardCode}</Badge></div>}
          <div className="small dim mt16">Thành viên từ {fmtDate(user!.createdAt)}</div>
        </div>
        <div className="card span2">
          <div className="card-head"><h2>Thông tin cá nhân</h2></div>
          <div className="form-grid">
            <div className="field"><label>Họ và tên</label><input className="input" value={f.fullName} onChange={set('fullName')} id="profile-name" /></div>
            <div className="field"><label>Email</label><input className="input" value={f.email} disabled /></div>
            <div className="field"><label>Số điện thoại</label><input className="input" value={f.phone} onChange={set('phone')} id="profile-phone" /></div>
            <div className="field"><label>Ngày sinh</label><input type="date" className="input" value={f.dob} onChange={set('dob')} /></div>
            <div className="field"><label>Giới tính</label><select className="input" value={f.gender} onChange={set('gender')}><option value="male">Nam</option><option value="female">Nữ</option><option value="other">Khác</option></select></div>
            <div className="field"><label>Địa chỉ</label><input className="input" value={f.address} onChange={set('address')} /></div>
          </div>
          <div className="row mt16" style={{ justifyContent: 'flex-end' }}><button className="btn primary" onClick={save} id="profile-save"><Save size={16} /> Lưu thay đổi</button></div>
          <div className="divider" />
          <h3 className="mb16">Đổi mật khẩu</h3>
          <div className="grid g3" style={{ gap: 12 }}>
            <input type="password" className="input" placeholder="Mật khẩu hiện tại" value={pw.old} onChange={(e) => setPw({ ...pw, old: e.target.value })} />
            <input type="password" className="input" placeholder="Mật khẩu mới" value={pw.n1} onChange={(e) => setPw({ ...pw, n1: e.target.value })} />
            <input type="password" className="input" placeholder="Xác nhận" value={pw.n2} onChange={(e) => setPw({ ...pw, n2: e.target.value })} />
          </div>
          <div className="row mt16" style={{ justifyContent: 'flex-end' }}><button className="btn" onClick={changePw}>Cập nhật mật khẩu</button></div>
        </div>
      </div>

      {/* Thẻ hội viên & Mã QR (qrcode.react) + Báo mất thẻ */}
      <div className="mt24">
        <MemberCardWidget memberId={user!.id} showHistory />
      </div>
    </>
  );
}

type MetricKey = 'weight' | 'bodyFat' | 'muscleMass' | 'bmi' | 'waist' | 'chest' | 'hips';

interface ChartClickState {
  activePayload?: Array<{ payload?: BodyMetric }>;
}

/* ================= Tiến độ & Chỉ số InBody ================= */
export function MemberProgress() {
  const { db, user, act, toast } = useApp();
  const [openAddModal, setOpenAddModal] = useState(false);
  const [inspectedMetric, setInspectedMetric] = useState<BodyMetric | null>(null);
  const [selectedMilestoneId, setSelectedMilestoneId] = useState<string | null>(null);
  const [metricKey, setMetricKey] = useState<MetricKey>('weight');
  const [compareKey, setCompareKey] = useState<MetricKey | 'none'>('muscleMass');
  const [apiMeasurements, setApiMeasurements] = useState<ApiBodyMeasurementDto[] | null>(null);
  const [loading, setLoading] = useState(false);

  const handleDeleteMeasurement = async (id: string) => {
    if (getAuthToken() && apiMeasurements !== null) {
      try {
        await bodyMeasurementsApi.deleteMeasurement(id);
        toast('Đã xóa bản ghi mốc đo', 'success');
        await fetchMeasurements();
        return;
      } catch (err) {
        if (!isApiNetworkError(err)) {
          toast((err as Error).message, 'error');
          return;
        }
      }
    }
    act((d) => {
      d.bodyMetrics = d.bodyMetrics.filter((x) => x.id !== id);
    }, 'Đã xóa bản ghi mốc đo');
  };

  const fetchMeasurements = useCallback(async () => {
    setLoading(true);
    try {
      const data = await bodyMeasurementsApi.getMyMeasurements();
      setApiMeasurements(data);
    } catch {
      // offline fallback
      setApiMeasurements(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchMeasurements();
  }, [fetchMeasurements]);

  const list: BodyMetric[] = useMemo(() => {
    if (apiMeasurements && apiMeasurements.length > 0) {
      return apiMeasurements.map((m) => ({
        id: m.id,
        memberId: m.memberId,
        date: m.measuredAt.slice(0, 10),
        weight: Number(m.weight),
        height: Number(m.height || 175),
        bmi: Number(m.bmi || 24),
        bodyFat: m.bodyFat ? Number(m.bodyFat) : undefined,
        muscleMass: m.muscleMass ? Number(m.muscleMass) : undefined,
        waist: m.waist ? Number(m.waist) : undefined,
        chest: m.chest ? Number(m.chest) : undefined,
        hips: m.hips ? Number(m.hips) : undefined,
        note: m.note,
      })).sort((a, b) => a.date.localeCompare(b.date));
    }
    return db.bodyMetrics
      .filter((m) => m.memberId === user!.id)
      .sort((a, b) => a.date.localeCompare(b.date));
  }, [apiMeasurements, db.bodyMetrics, user]);

  const first = list[0];
  const last = list.at(-1);
  const activeMilestone = useMemo(() => {
    if (!list.length) return undefined;
    return list.find((m) => m.id === selectedMilestoneId) ?? list.at(-1);
  }, [list, selectedMilestoneId]);

  const delta = (k: keyof BodyMetric) => {
    if (!first || !last) return 0;
    const v1 = first[k] as number | undefined;
    const v2 = last[k] as number | undefined;
    return v1 != null && v2 != null ? +(v2 - v1).toFixed(1) : 0;
  };

  const meta: Record<MetricKey, { label: string; color: string; unit: string }> = {
    weight: { label: 'Cân nặng (kg)', color: '#4f46e5', unit: 'kg' },
    bmi: { label: 'Chỉ số BMI', color: '#d97706', unit: '' },
    bodyFat: { label: 'Tỷ lệ mỡ (%)', color: '#dc2626', unit: '%' },
    muscleMass: { label: 'Khối lượng cơ (kg)', color: '#0d9488', unit: 'kg' },
    waist: { label: 'Vòng eo (cm)', color: '#0284c7', unit: 'cm' },
    chest: { label: 'Vòng ngực (cm)', color: '#7c3aed', unit: 'cm' },
    hips: { label: 'Vòng mông (cm)', color: '#db2777', unit: 'cm' },
  };

  const activeIdx = activeMilestone ? list.findIndex((m) => m.id === activeMilestone.id) : -1;
  const prevMilestone = activeIdx > 0 ? list[activeIdx - 1] : undefined;

  const diffFrom = (target: BodyMetric | undefined, base: BodyMetric | undefined, key: MetricKey): string => {
    if (!target || !base) return '—';
    const vNow = target[key];
    const vBase = base[key];
    if (typeof vNow !== 'number' || typeof vBase !== 'number') return '—';
    const d = +(vNow - vBase).toFixed(1);
    return `${d > 0 ? '+' : ''}${d}${meta[key].unit}`;
  };

  return (
    <>
      <PageHead
        title="Tiến Độ Thể Hình, Biểu Đồ Mốc Đo"
        desc={
          first
            ? `Lộ trình từ ${fmtDate(first.date)} đến ${fmtDate(last!.date)} · Đã lưu ${list.length} mốc đo InBody.`
            : 'Ghi nhận mốc đo đầu tiên để theo dõi lộ trình thay đổi chỉ số cơ thể InBody.'
        }
        actions={
          <button className="btn primary" onClick={() => setOpenAddModal(true)} id="add-metric">
            <Plus size={16} /> Thêm chỉ số / Mốc đo mới
          </button>
        }
      />

      {!last ? (
          <div className="card" style={{ padding: 48, textAlign: 'center' }}>
            <Empty
              text="Chưa có mốc đo nào được lưu. Hãy bấm 'Thêm chỉ số / Mốc đo mới' để bắt đầu vẽ biểu đồ tiến độ!"
              icon={<Activity size={48} />}
            />
            <button className="btn primary mt16" onClick={() => setOpenAddModal(true)}>
              <Plus size={16} /> Nhập số đo ban đầu
            </button>
          </div>
        ) : (
          <>
            {/* THẺ THỐNG KÊ NHANH CÁC CHỈ SỐ QUAN TRỌNG */}
            <div className="grid g4 mb16">
              <StatCard
                icon={<Scale size={22} />}
                label="Cân nặng hiện tại"
                value={`${last.weight} kg`}
                color="#4f46e5"
                trend={
                  list.length > 1
                    ? { v: `${delta('weight') > 0 ? '+' : ''}${delta('weight')} kg so với đầu kỳ`, up: delta('weight') <= 0 }
                    : undefined
                }
              />
              <StatCard
                icon={<Ruler size={22} />}
                label={`BMI · ${bmiLabel(last.bmi)}`}
                value={last.bmi}
                color="#d97706"
                trend={
                  list.length > 1
                    ? { v: `${delta('bmi') > 0 ? '+' : ''}${delta('bmi')} điểm`, up: delta('bmi') <= 0 }
                    : undefined
                }
              />
              <StatCard
                icon={<Percent size={22} />}
                label="Tỷ lệ mỡ cơ thể"
                value={last.bodyFat != null ? `${last.bodyFat}%` : '—'}
                color="#dc2626"
                trend={
                  list.length > 1 && last.bodyFat != null
                    ? { v: `${delta('bodyFat') > 0 ? '+' : ''}${delta('bodyFat')}% mỡ`, up: delta('bodyFat') <= 0 }
                    : undefined
                }
              />
              <StatCard
                icon={<Dumbbell size={22} />}
                label="Khối lượng cơ nạc"
                value={last.muscleMass != null ? `${last.muscleMass} kg` : '—'}
                color="#0d9488"
                trend={
                  list.length > 1 && last.muscleMass != null
                    ? { v: `${delta('muscleMass') > 0 ? '+' : ''}${delta('muscleMass')} kg cơ`, up: delta('muscleMass') >= 0 }
                    : undefined
                }
              />
            </div>

            {/* KHỐI BIỂU ĐỒ TƯƠNG TÁC + SO SÁNH KÉP */}
            <div className="card mb16">
              <div className="card-head between wrap" style={{ gap: 12 }}>
                <div>
                  <h2>
                    <Activity size={18} /> Biểu đồ theo dõi tiến độ qua từng mốc đo
                  </h2>
                  <span className="small muted">
                    Chọn chỉ số muốn vẽ biểu đồ hoặc bấm trực tiếp vào bất kỳ mốc đo nào để xem lịch sử chi tiết
                  </span>
                </div>

                <div className="row wrap" style={{ gap: 8 }}>
                  <span className="small muted">So sánh kèm:</span>
                  <select
                    className="input sm"
                    style={{ width: 'auto', minWidth: 165 }}
                    value={compareKey}
                    onChange={(e) => setCompareKey(e.target.value as MetricKey | 'none')}
                  >
                    <option value="none">— Không so sánh phụ —</option>
                    {(Object.keys(meta) as MetricKey[])
                      .filter((k) => k !== metricKey)
                      .map((k) => (
                        <option key={k} value={k}>
                          + {meta[k].label}
                        </option>
                      ))}
                  </select>
                </div>
              </div>

              {/* Danh sách chỉ số chính */}
              <div className="chips mb12">
                {(Object.keys(meta) as MetricKey[]).map((k) => (
                  <button
                    key={k}
                    className={`chip ${metricKey === k ? 'on' : ''}`}
                    onClick={() => {
                      setMetricKey(k);
                      if (compareKey === k) setCompareKey('none');
                    }}
                  >
                    {meta[k].label}
                  </button>
                ))}
              </div>

              <div style={{ height: 320, width: '100%', marginTop: 4 }}>
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart
                    data={list.map((m, idx) => ({
                      ...m,
                      d: `Lần ${idx + 1} (${fmtDate(m.date).slice(0, 5)})`,
                    }))}
                    onClick={(state: unknown) => {
                      const st = state as ChartClickState | null;
                      const clicked = st?.activePayload?.[0]?.payload;
                      if (clicked) {
                        setSelectedMilestoneId(clicked.id);
                      }
                    }}
                  >
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--border)" />
                    <XAxis dataKey="d" axisLine={false} tickLine={false} tick={{ fill: 'var(--text-dim)', fontSize: 12 }} />
                    <YAxis
                      yAxisId="left"
                      domain={['dataMin - 1', 'dataMax + 1']}
                      axisLine={false}
                      tickLine={false}
                      width={45}
                      tick={{ fill: meta[metricKey].color, fontSize: 12, fontWeight: 700 }}
                    />
                    {compareKey !== 'none' && (
                      <YAxis
                        yAxisId="right"
                        orientation="right"
                        domain={['dataMin - 1', 'dataMax + 1']}
                        axisLine={false}
                        tickLine={false}
                        width={45}
                        tick={{ fill: meta[compareKey].color, fontSize: 12, fontWeight: 700 }}
                      />
                    )}
                    <Tooltip
                      content={({ active, payload }) => {
                        if (active && payload && payload.length) {
                          const data = payload[0].payload as BodyMetric;
                          const idx = list.findIndex((x) => x.id === data.id);
                          return (
                            <div
                              className="card"
                              style={{
                                padding: '12px 14px',
                                background: 'var(--surface)',
                                border: '1px solid var(--border-strong)',
                                boxShadow: 'var(--shadow)',
                              }}
                            >
                              <div className="bold mb4" style={{ color: 'var(--primary)' }}>
                                📅 Mốc #{idx + 1}: {fmtDate(data.date)}
                              </div>
                              <div className="small">
                                Cân nặng: <b>{data.weight} kg</b> · BMI: <b>{data.bmi}</b>
                              </div>
                              {data.bodyFat != null && (
                                <div className="small">
                                  Tỷ lệ mỡ: <b>{data.bodyFat}%</b> · Cơ nạc: <b>{data.muscleMass ?? '—'} kg</b>
                                </div>
                              )}
                              {(data.chest || data.waist || data.hips) && (
                                <div className="small">
                                  Số đo 3 vòng: <b>{data.chest ?? '—'} - {data.waist ?? '—'} - {data.hips ?? '—'} cm</b>
                                </div>
                              )}
                              {data.note && <div className="small muted mt4">“{data.note}”</div>}
                              <div className="small bold mt8" style={{ color: 'var(--primary)' }}>
                                👉 Click để ghim xem mốc đo này bên dưới
                              </div>
                            </div>
                          );
                        }
                        return null;
                      }}
                    />
                    <Legend />
                    <Line
                      yAxisId="left"
                      type="monotone"
                      dataKey={metricKey}
                      name={meta[metricKey].label}
                      stroke={meta[metricKey].color}
                      strokeWidth={3}
                      dot={{ r: 6, fill: meta[metricKey].color, strokeWidth: 2, stroke: 'var(--surface)' }}
                      activeDot={{ r: 9, stroke: '#fff', strokeWidth: 2 }}
                    />
                    {compareKey !== 'none' && (
                      <Line
                        yAxisId="right"
                        type="monotone"
                        dataKey={compareKey}
                        name={meta[compareKey].label}
                        stroke={meta[compareKey].color}
                        strokeWidth={2.5}
                        strokeDasharray="5 5"
                        dot={{ r: 5, fill: meta[compareKey].color, strokeWidth: 2, stroke: 'var(--surface)' }}
                      />
                    )}
                  </LineChart>
                </ResponsiveContainer>
              </div>

              {/* THANH CHỌN NHANH TỪNG MỐC ĐO LỊCH SỬ (MILESTONE TIMELINE SCRUBBER) */}
              <div style={{ marginTop: 14, paddingTop: 14, borderTop: '1px solid var(--border)' }}>
                <div className="row between wrap mb8">
                  <span className="small bold">
                    🎯 Chọn mốc đo lịch sử để xem chi tiết & so sánh ({list.length} mốc):
                  </span>
                  <span className="small muted">Bấm vào từng mốc bên dưới hoặc chấm tròn trên biểu đồ</span>
                </div>
                <div className="row wrap" style={{ gap: 8 }}>
                  {list.map((m, idx) => {
                    const isSel = activeMilestone?.id === m.id;
                    return (
                      <button
                        key={m.id}
                        type="button"
                        onClick={() => setSelectedMilestoneId(m.id)}
                        className="card hover"
                        style={{
                          padding: '8px 12px',
                          cursor: 'pointer',
                          borderRadius: 10,
                          border: isSel ? '2px solid var(--primary)' : '1px solid var(--border)',
                          background: isSel ? 'var(--primary-soft)' : 'var(--surface-2)',
                          textAlign: 'left',
                        }}
                      >
                        <div className="small bold" style={{ color: isSel ? 'var(--primary)' : 'var(--text)' }}>
                          Lần {idx + 1} · {fmtDate(m.date).slice(0, 5)}
                        </div>
                        <div className="small dim" style={{ fontSize: 11 }}>
                          {m[metricKey] ?? '—'} {meta[metricKey].unit}
                        </div>
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* BẢNG PHÂN TÍCH MỐC ĐO ĐANG CHỌN (INLINE MILESTONE INSPECTOR) */}
              {activeMilestone && (
                <div
                  className="card mt16"
                  style={{
                    background: 'var(--surface-2)',
                    border: '1px solid var(--border-strong)',
                    padding: 16,
                  }}
                >
                  <div className="row between wrap mb12" style={{ gap: 10 }}>
                    <div className="row wrap" style={{ gap: 10 }}>
                      <Badge color="purple">
                        Đang xem Mốc #{activeIdx + 1}/{list.length}
                      </Badge>
                      <h3 style={{ fontSize: 16 }}>Ngày đo: {fmtDate(activeMilestone.date)}</h3>
                      {activeMilestone.id === first.id && <Badge color="amber" plain>Khởi điểm (Baseline)</Badge>}
                      {activeMilestone.id === last.id && <Badge color="green">Mới nhất</Badge>}
                    </div>
                    <div className="row" style={{ gap: 8 }}>
                      <button
                        className="btn sm"
                        onClick={() => setInspectedMetric(activeMilestone)}
                      >
                        <Eye size={14} /> Mở bảng so sánh đầy đủ
                      </button>
                    </div>
                  </div>

                  <div className="grid grid-4" style={{ gap: 10 }}>
                    <div className="card" style={{ padding: '10px 12px' }}>
                      <div className="small muted">Cân nặng & BMI</div>
                      <div className="bold" style={{ fontSize: 16, marginTop: 2 }}>
                        {activeMilestone.weight} kg · BMI {activeMilestone.bmi}
                      </div>
                      <div className="small dim mt4">
                        So với lần trước: <b>{diffFrom(activeMilestone, prevMilestone, 'weight')}</b> · So với đầu kỳ:{' '}
                        <b>{diffFrom(activeMilestone, first, 'weight')}</b>
                      </div>
                    </div>

                    <div className="card" style={{ padding: '10px 12px' }}>
                      <div className="small muted">Tỷ lệ mỡ cơ thể</div>
                      <div className="bold" style={{ fontSize: 16, marginTop: 2, color: 'var(--red)' }}>
                        {activeMilestone.bodyFat != null ? `${activeMilestone.bodyFat}%` : '—'}
                      </div>
                      <div className="small dim mt4">
                        So với lần trước: <b>{diffFrom(activeMilestone, prevMilestone, 'bodyFat')}</b> · So với đầu kỳ:{' '}
                        <b>{diffFrom(activeMilestone, first, 'bodyFat')}</b>
                      </div>
                    </div>

                    <div className="card" style={{ padding: '10px 12px' }}>
                      <div className="small muted">Khối lượng cơ nạc</div>
                      <div className="bold" style={{ fontSize: 16, marginTop: 2, color: 'var(--accent)' }}>
                        {activeMilestone.muscleMass != null ? `${activeMilestone.muscleMass} kg` : '—'}
                      </div>
                      <div className="small dim mt4">
                        So với lần trước: <b>{diffFrom(activeMilestone, prevMilestone, 'muscleMass')}</b> · So với đầu kỳ:{' '}
                        <b>{diffFrom(activeMilestone, first, 'muscleMass')}</b>
                      </div>
                    </div>

                    <div className="card" style={{ padding: '10px 12px' }}>
                      <div className="small muted">Số đo 3 vòng (Ngực - Eo - Mông)</div>
                      <div className="bold" style={{ fontSize: 16, marginTop: 2, color: 'var(--primary)' }}>
                        {activeMilestone.chest ?? '—'} / {activeMilestone.waist ?? '—'} / {activeMilestone.hips ?? '—'} cm
                      </div>
                      <div className="small dim mt4">
                        Vòng eo so với đầu kỳ: <b>{diffFrom(activeMilestone, first, 'waist')}</b>
                      </div>
                    </div>
                  </div>

                  {activeMilestone.note && (
                    <div className="small muted mt12" style={{ padding: '8px 12px', background: 'var(--surface)', borderRadius: 8 }}>
                      📝 <b>Ghi chú huấn luyện tại mốc này:</b> “{activeMilestone.note}”
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* BẢNG LỊCH SỬ TỪNG MỐC ĐO */}
            <div className="card">
              <div className="card-head between">
                <div>
                  <h2>
                    <History size={18} /> Bảng lịch sử toàn bộ các mốc đo ({list.length})
                  </h2>
                  <span className="small muted">Bấm vào bất kỳ dòng nào để xem chi tiết hoặc thêm mốc đo mới</span>
                </div>
                <button className="btn primary sm" onClick={() => setOpenAddModal(true)}>
                  <Plus size={15} /> Thêm mốc đo mới
                </button>
              </div>

              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Mốc đo</th>
                      <th>Cân nặng</th>
                      <th>BMI</th>
                      <th>% Mỡ</th>
                      <th>Cơ nạc</th>
                      <th>3 vòng (Ngực / Eo / Mông)</th>
                      <th>Ghi chú lộ trình</th>
                      <th style={{ textAlign: 'right' }}>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[...list].reverse().map((m, revIdx) => {
                      const chronologicalIdx = list.length - revIdx;
                      const isFirst = m.id === first.id;
                      const isLatest = m.id === last.id;
                      const isSelected = activeMilestone?.id === m.id;
                      return (
                        <tr
                          key={m.id}
                          style={{
                            cursor: 'pointer',
                            background: isSelected ? 'var(--primary-soft)' : undefined,
                          }}
                          onClick={() => {
                            setSelectedMilestoneId(m.id);
                            setInspectedMetric(m);
                          }}
                        >
                          <td>
                            <div className="row" style={{ gap: 6 }}>
                              <Badge color="slate" plain>
                                #{chronologicalIdx}
                              </Badge>
                              <b>{fmtDate(m.date)}</b>
                              {isLatest && <Badge color="green">Mới nhất</Badge>}
                              {isFirst && (
                                <Badge color="purple" plain>
                                  Khởi điểm
                                </Badge>
                              )}
                            </div>
                          </td>
                          <td>
                            <b>{m.weight}</b> kg
                          </td>
                          <td>
                            <Badge color={m.bmi >= 18.5 && m.bmi <= 23 ? 'green' : m.bmi > 25 ? 'red' : 'amber'}>
                              {m.bmi} · {bmiLabel(m.bmi)}
                            </Badge>
                          </td>
                          <td>{m.bodyFat != null ? `${m.bodyFat}%` : '—'}</td>
                          <td>{m.muscleMass != null ? `${m.muscleMass} kg` : '—'}</td>
                          <td className="small">
                            {m.chest || m.waist || m.hips ? (
                              <span>
                                {m.chest ?? '—'} / <b>{m.waist ?? '—'}</b> / {m.hips ?? '—'} cm
                              </span>
                            ) : (
                              <span className="dim">—</span>
                            )}
                          </td>
                          <td className="small muted truncate" style={{ maxWidth: 210 }}>
                            {m.note || '—'}
                          </td>
                          <td style={{ textAlign: 'right' }}>
                            <div className="row" style={{ justifyContent: 'flex-end', gap: 6 }}>
                              <button
                                className="btn sm ghost"
                                title="Xem phân tích chi tiết mốc này"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setInspectedMetric(m);
                                }}
                              >
                                <Eye size={15} /> So sánh
                              </button>
                              <button
                                className="btn sm ghost"
                                title="Xóa bản ghi này"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  handleDeleteMeasurement(m.id);
                                }}
                              >
                                <Trash2 size={15} color="var(--red)" />
                              </button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          </>
      )}

      {/* MODAL THÊM CHỈ SỐ MỚI */}
      {openAddModal && (
        <MetricModal
          lastHeight={last?.height}
          lastWeight={last?.weight}
          lastBodyFat={last?.bodyFat}
          lastMuscleMass={last?.muscleMass}
          lastChest={last?.chest}
          lastWaist={last?.waist}
          lastHips={last?.hips}
          onSaved={fetchMeasurements}
          onClose={() => setOpenAddModal(false)}
        />
      )}

      {/* MODAL XEM CHI TIẾT & SO SÁNH MỐC ĐO LỊCH SỬ */}
      {inspectedMetric && (
        <CheckpointDetailModal
          checkpoint={inspectedMetric}
          baseline={first!}
          allMetrics={list}
          onClose={() => setInspectedMetric(null)}
        />
      )}
    </>
  );
}

/* ================= MODAL XEM CHI TIẾT & SO SÁNH MỐC ĐO ================= */
function CheckpointDetailModal({
  checkpoint,
  baseline,
  allMetrics,
  onClose,
}: {
  checkpoint: BodyMetric;
  baseline: BodyMetric;
  allMetrics: BodyMetric[];
  onClose: () => void;
}) {
  const currentIndex = allMetrics.findIndex((m) => m.id === checkpoint.id);
  const prevCheckpoint = currentIndex > 0 ? allMetrics[currentIndex - 1] : null;

  // Tính delta so với mốc ban đầu (Baseline)
  const deltaBase = (key: keyof BodyMetric) => {
    const vNow = checkpoint[key] as number | undefined;
    const vBase = baseline[key] as number | undefined;
    if (vNow == null || vBase == null) return null;
    return +(vNow - vBase).toFixed(1);
  };

  // Tính delta so với mốc liền trước (Previous)
  const deltaPrev = (key: keyof BodyMetric) => {
    if (!prevCheckpoint) return null;
    const vNow = checkpoint[key] as number | undefined;
    const vPrev = prevCheckpoint[key] as number | undefined;
    if (vNow == null || vPrev == null) return null;
    return +(vNow - vPrev).toFixed(1);
  };

  const weightDeltaBase = deltaBase('weight');
  const fatDeltaBase = deltaBase('bodyFat');
  const muscleDeltaBase = deltaBase('muscleMass');

  return (
    <Modal
      title={`Chi tiết mốc đo: ${fmtDate(checkpoint.date)}`}
      onClose={onClose}
      footer={<button className="btn primary" onClick={onClose}>Đóng</button>}
    >
      <div className="col" style={{ gap: 16 }}>
        {/* THÔNG TIN CHUNG MỐC ĐO */}
        <div className="card" style={{ background: 'var(--surface-2)', padding: 14 }}>
          <div className="row between wrap" style={{ gap: 8 }}>
            <div>
              <span className="small muted">Mốc đo thứ {currentIndex + 1} / {allMetrics.length}</span>
              <h3 style={{ fontSize: 18, marginTop: 2 }}>{fmtDate(checkpoint.date)}</h3>
            </div>
            <div className="row" style={{ gap: 6 }}>
              {checkpoint.id === baseline.id ? (
                <Badge color="purple">Mốc ban đầu (Baseline)</Badge>
              ) : currentIndex === allMetrics.length - 1 ? (
                <Badge color="green">Mốc mới nhất</Badge>
              ) : (
                <Badge color="blue">Mốc giữa kỳ</Badge>
              )}
            </div>
          </div>
          {checkpoint.note && (
            <div className="small muted mt8" style={{ fontStyle: 'italic', borderTop: '1px solid var(--border)', paddingTop: 8 }}>
              💬 Ghi chú: "{checkpoint.note}"
            </div>
          )}
        </div>

        {/* BẢNG SO SÁNH CHỈ SỐ: HIỆN TẠI VS BAN ĐẦU VS LẦN ĐO TRƯỚC */}
        <div>
          <h4 className="mb8" style={{ fontSize: 14 }}>📊 Bảng so sánh các chỉ số</h4>
          <div className="table-wrap">
            <table style={{ fontSize: 13 }}>
              <thead>
                <tr>
                  <th>Chỉ số</th>
                  <th>Giá trị mốc này</th>
                  <th>So với lần trước</th>
                  <th>So với ban đầu</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td><b>Cân nặng</b></td>
                  <td><b>{checkpoint.weight} kg</b></td>
                  <td>
                    {deltaPrev('weight') != null ? (
                      <span style={{ color: deltaPrev('weight')! <= 0 ? 'var(--green)' : 'var(--amber)', fontWeight: 600 }}>
                        {deltaPrev('weight')! > 0 ? `+${deltaPrev('weight')}` : deltaPrev('weight')} kg
                      </span>
                    ) : '—'}
                  </td>
                  <td>
                    {weightDeltaBase != null ? (
                      <span style={{ color: weightDeltaBase <= 0 ? 'var(--green)' : 'var(--amber)', fontWeight: 700 }}>
                        {weightDeltaBase > 0 ? `+${weightDeltaBase}` : weightDeltaBase} kg
                      </span>
                    ) : '—'}
                  </td>
                </tr>

                <tr>
                  <td><b>BMI</b></td>
                  <td><b>{checkpoint.bmi}</b> ({bmiLabel(checkpoint.bmi)})</td>
                  <td>
                    {deltaPrev('bmi') != null ? (
                      <span>{deltaPrev('bmi')! > 0 ? `+${deltaPrev('bmi')}` : deltaPrev('bmi')}</span>
                    ) : '—'}
                  </td>
                  <td>
                    {deltaBase('bmi') != null ? (
                      <span style={{ fontWeight: 600 }}>{deltaBase('bmi')! > 0 ? `+${deltaBase('bmi')}` : deltaBase('bmi')}</span>
                    ) : '—'}
                  </td>
                </tr>

                <tr>
                  <td><b>Tỷ lệ mỡ (%)</b></td>
                  <td>{checkpoint.bodyFat != null ? `${checkpoint.bodyFat}%` : '—'}</td>
                  <td>
                    {deltaPrev('bodyFat') != null ? (
                      <span style={{ color: deltaPrev('bodyFat')! <= 0 ? 'var(--green)' : 'var(--red)', fontWeight: 600 }}>
                        {deltaPrev('bodyFat')! > 0 ? `+${deltaPrev('bodyFat')}` : deltaPrev('bodyFat')}%
                      </span>
                    ) : '—'}
                  </td>
                  <td>
                    {fatDeltaBase != null ? (
                      <span style={{ color: fatDeltaBase <= 0 ? 'var(--green)' : 'var(--red)', fontWeight: 700 }}>
                        {fatDeltaBase > 0 ? `+${fatDeltaBase}` : fatDeltaBase}%
                      </span>
                    ) : '—'}
                  </td>
                </tr>

                <tr>
                  <td><b>Khối lượng cơ</b></td>
                  <td>{checkpoint.muscleMass != null ? `${checkpoint.muscleMass} kg` : '—'}</td>
                  <td>
                    {deltaPrev('muscleMass') != null ? (
                      <span style={{ color: deltaPrev('muscleMass')! >= 0 ? 'var(--green)' : 'var(--amber)', fontWeight: 600 }}>
                        {deltaPrev('muscleMass')! > 0 ? `+${deltaPrev('muscleMass')}` : deltaPrev('muscleMass')} kg
                      </span>
                    ) : '—'}
                  </td>
                  <td>
                    {muscleDeltaBase != null ? (
                      <span style={{ color: muscleDeltaBase >= 0 ? 'var(--green)' : 'var(--amber)', fontWeight: 700 }}>
                        {muscleDeltaBase > 0 ? `+${muscleDeltaBase}` : muscleDeltaBase} kg
                      </span>
                    ) : '—'}
                  </td>
                </tr>

                {(checkpoint.waist != null || baseline.waist != null) && (
                  <tr>
                    <td><b>Vòng eo</b></td>
                    <td>{checkpoint.waist != null ? `${checkpoint.waist} cm` : '—'}</td>
                    <td>
                      {deltaPrev('waist') != null ? (
                        <span>{deltaPrev('waist')! > 0 ? `+${deltaPrev('waist')}` : deltaPrev('waist')} cm</span>
                      ) : '—'}
                    </td>
                    <td>
                      {deltaBase('waist') != null ? (
                        <span style={{ fontWeight: 600 }}>{deltaBase('waist')! > 0 ? `+${deltaBase('waist')}` : deltaBase('waist')} cm</span>
                      ) : '—'}
                    </td>
                  </tr>
                )}

                {(checkpoint.chest != null || baseline.chest != null) && (
                  <tr>
                    <td><b>Vòng ngực</b></td>
                    <td>{checkpoint.chest != null ? `${checkpoint.chest} cm` : '—'}</td>
                    <td>
                      {deltaPrev('chest') != null ? (
                        <span>{deltaPrev('chest')! > 0 ? `+${deltaPrev('chest')}` : deltaPrev('chest')} cm</span>
                      ) : '—'}
                    </td>
                    <td>
                      {deltaBase('chest') != null ? (
                        <span style={{ fontWeight: 600 }}>{deltaBase('chest')! > 0 ? `+${deltaBase('chest')}` : deltaBase('chest')} cm</span>
                      ) : '—'}
                    </td>
                  </tr>
                )}

                {(checkpoint.hips != null || baseline.hips != null) && (
                  <tr>
                    <td><b>Vòng mông</b></td>
                    <td>{checkpoint.hips != null ? `${checkpoint.hips} cm` : '—'}</td>
                    <td>
                      {deltaPrev('hips') != null ? (
                        <span>{deltaPrev('hips')! > 0 ? `+${deltaPrev('hips')}` : deltaPrev('hips')} cm</span>
                      ) : '—'}
                    </td>
                    <td>
                      {deltaBase('hips') != null ? (
                        <span style={{ fontWeight: 600 }}>{deltaBase('hips')! > 0 ? `+${deltaBase('hips')}` : deltaBase('hips')} cm</span>
                      ) : '—'}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* NHẬN XÉT TIẾN ĐỘ THỂ HÌNH */}
        <div className="card" style={{ background: 'rgba(124, 92, 255, 0.08)', borderColor: 'rgba(124, 92, 255, 0.3)', padding: 14 }}>
          <div className="row" style={{ gap: 8, alignItems: 'center', marginBottom: 6 }}>
            <Sparkles size={16} color="var(--primary)" />
            <span className="bold">Đánh giá tiến độ từ hệ thống OOAD:</span>
          </div>
          <p className="small muted" style={{ lineHeight: 1.5 }}>
            {weightDeltaBase != null && weightDeltaBase < 0 && muscleDeltaBase != null && muscleDeltaBase >= 0
              ? '🎉 Tuyệt vời! Bạn đang giảm mỡ đồng thời bảo toàn/tăng khối lượng cơ nạc. Đây là trạng thái Body Recomposition lý tưởng nhất.'
              : weightDeltaBase != null && weightDeltaBase < 0
              ? '👍 Bạn đang giảm cân ổn định. Tiếp tục duy trì lượng Protein để bảo toàn cơ bắp nhé.'
              : muscleDeltaBase != null && muscleDeltaBase > 0
              ? '💪 Bạn đang tăng trưởng khối lượng cơ bắp hiệu quả. Hãy duy trì thặng dư calo vừa phải.'
              : '🌟 Cơ thể đang duy trì trạng thái ổn định. Tiếp tục duy trì lịch tập đều đặn để bứt phá mục tiêu.'}
          </p>
        </div>
      </div>
    </Modal>
  );
}

/* ================= MODAL NHẬP MỐC ĐO CHỈ SỐ MỚI ================= */
function MetricModal({
  lastHeight,
  lastWeight,
  lastBodyFat,
  lastMuscleMass,
  lastChest,
  lastWaist,
  lastHips,
  onSaved,
  onClose,
}: {
  lastHeight?: number;
  lastWeight?: number;
  lastBodyFat?: number;
  lastMuscleMass?: number;
  lastChest?: number;
  lastWaist?: number;
  lastHips?: number;
  onSaved?: () => void;
  onClose: () => void;
}) {
  const { user, act, toast } = useApp();
  const [f, setF] = useState({
    date: todayStr(),
    weight: String(lastWeight ?? ''),
    height: String(lastHeight ?? ''),
    bodyFat: String(lastBodyFat ?? ''),
    muscleMass: String(lastMuscleMass ?? ''),
    chest: String(lastChest ?? ''),
    waist: String(lastWaist ?? ''),
    hips: String(lastHips ?? ''),
    note: '',
  });

  const bmi = calcBMI(+f.weight, +f.height);

  const save = async () => {
    if (!+f.weight || !+f.height) {
      toast('Vui lòng nhập cân nặng và chiều cao hợp lệ', 'error');
      return;
    }

    try {
      await bodyMeasurementsApi.createMeasurement({
        weight: +f.weight,
        height: +f.height,
        bmi,
        bodyFat: f.bodyFat ? +f.bodyFat : undefined,
        muscleMass: f.muscleMass ? +f.muscleMass : undefined,
        chest: f.chest ? +f.chest : undefined,
        waist: f.waist ? +f.waist : undefined,
        hips: f.hips ? +f.hips : undefined,
        note: f.note,
        measuredAt: f.date ? `${f.date}T00:00:00Z` : undefined,
      });
      toast('Đã lưu mốc đo chỉ số cơ thể thành công', 'success');
      onSaved?.();
      onClose();
    } catch {
      const r = act((d) => {
        d.bodyMetrics.push({
          id: genId('bm'),
          memberId: user!.id,
          date: f.date,
          weight: +f.weight,
          height: +f.height,
          bmi,
          bodyFat: f.bodyFat ? +f.bodyFat : undefined,
          muscleMass: f.muscleMass ? +f.muscleMass : undefined,
          chest: f.chest ? +f.chest : undefined,
          waist: f.waist ? +f.waist : undefined,
          hips: f.hips ? +f.hips : undefined,
          note: f.note,
        });
        return true;
      }, 'Đã lưu mốc đo chỉ số cơ thể');
      if (r) {
        onSaved?.();
        onClose();
      }
    }
  };

  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value });

  return (
    <Modal
      title="Ghi nhận mốc đo cơ thể mới"
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose}>Hủy</button>
          <button className="btn primary" onClick={save} id="metric-save">
            <Save size={15} /> Lưu chỉ số & Cập nhật biểu đồ
          </button>
        </>
      }
    >
      <div className="col" style={{ gap: 14 }}>
        <div className="form-grid">
          <div className="field">
            <label>Ngày đo *</label>
            <input type="date" className="input" value={f.date} max={todayStr()} onChange={set('date')} />
          </div>

          <div className="field">
            <label>Cân nặng (kg) *</label>
            <input
              type="number"
              step="0.1"
              className="input"
              placeholder="VD: 68.5"
              value={f.weight}
              onChange={set('weight')}
              id="metric-weight"
            />
          </div>

          <div className="field">
            <label>Chiều cao (cm) *</label>
            <input
              type="number"
              className="input"
              placeholder="VD: 172"
              value={f.height}
              onChange={set('height')}
              id="metric-height"
            />
          </div>

          <div className="field">
            <label>BMI (tự tính tự động)</label>
            <input
              className="input"
              value={bmi ? `${bmi} · ${bmiLabel(bmi)}` : 'Chờ nhập cân nặng & chiều cao'}
              disabled
              style={{ fontWeight: 600, color: 'var(--primary)' }}
            />
          </div>

          <div className="field">
            <label>Tỷ lệ mỡ cơ thể (%)</label>
            <input
              type="number"
              step="0.1"
              className="input"
              placeholder="VD: 18.2"
              value={f.bodyFat}
              onChange={set('bodyFat')}
            />
          </div>

          <div className="field">
            <label>Khối lượng cơ (kg)</label>
            <input
              type="number"
              step="0.1"
              className="input"
              placeholder="VD: 31.5"
              value={f.muscleMass}
              onChange={set('muscleMass')}
            />
          </div>
        </div>

        {/* CÁC SỐ ĐO VÒNG (OPTIONAL ĐỂ VẼ BIỂU ĐỒ SỐ ĐO 3 VÒNG) */}
        <div style={{ borderTop: '1px solid var(--border)', paddingTop: 10 }}>
          <div className="small bold muted mb8">Số đo 3 vòng (cm) - Tùy chọn để theo dõi dáng</div>
          <div className="grid g3" style={{ gap: 10 }}>
            <div className="field">
              <label className="small">Vòng 1 - Ngực (cm)</label>
              <input type="number" step="0.5" className="input" placeholder="VD: 95" value={f.chest} onChange={set('chest')} />
            </div>
            <div className="field">
              <label className="small">Vòng 2 - Eo (cm)</label>
              <input type="number" step="0.5" className="input" placeholder="VD: 76" value={f.waist} onChange={set('waist')} />
            </div>
            <div className="field">
              <label className="small">Vòng 3 - Mông (cm)</label>
              <input type="number" step="0.5" className="input" placeholder="VD: 94" value={f.hips} onChange={set('hips')} />
            </div>
          </div>
        </div>

        <div className="field">
          <label>Ghi chú mốc đo</label>
          <input
            className="input"
            placeholder="VD: Đo buổi sáng sau khi ngủ dậy, sau 1 tháng tập giáo án mới..."
            value={f.note}
            onChange={set('note')}
          />
        </div>
      </div>
    </Modal>
  );
}

/* ================= Thẻ hội viên ================= */
export function MemberCard() {
  const { db, user } = useApp();
  const [apiMode, setApiMode] = useState<boolean>(!!getAuthToken());
  const [apiHistory, setApiHistory] = useState<ApiCheckinDto[]>([]);
  const [apiMemberships, setApiMemberships] = useState<ApiMembershipDto[]>([]);

  useEffect(() => {
    if (!getAuthToken()) {
      setApiMode(false);
      return;
    }
    let cancelled = false;
    Promise.all([
      checkinsApi.getMyHistory().catch(() => [] as ApiCheckinDto[]),
      membershipPlansApi.getMyMemberships().catch(() => [] as ApiMembershipDto[]),
    ])
      .then(([ciList, msList]) => {
        if (cancelled) return;
        setApiHistory(ciList);
        setApiMemberships(msList);
        setApiMode(true);
      })
      .catch(() => {
        if (!cancelled) setApiMode(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const cur = L.membershipOn(db, user!.id);
  const plan = cur ? L.planById(db, cur.planId) : undefined;
  const open = L.openCheckin(db, user!.id);
  const history = db.checkins.filter((c) => c.memberId === user!.id).slice(0, 12);

  const apiOpen = apiHistory.find((c) => !c.checkOutAt);
  const apiActiveGym = apiMemberships.find(
    (m) => m.planType.toUpperCase() !== 'PT' && m.status.toLowerCase() === 'active',
  );

  return (
    <>
      <PageHead title="Thẻ hội viên" desc="Đưa mã QR này vào máy quét tại quầy để check-in / check-out hoặc báo mất thẻ khẩn cấp." />
      <div className="grid g2">
        <div className="col" style={{ gap: 18 }}>
          <MemberCardWidget memberId={user!.id} showHistory />
          <div className="card">
            {apiMode ? (
              <>
                {apiOpen ? (
                  <div className="row">
                    <span className="live-dot" />
                    <div>
                      <b>Đang tập tại {apiOpen.branchName}</b>
                      <div className="small muted">Check-in lúc {fmtTime(apiOpen.checkInAt)} · nhớ check-out khi ra về (tự động check-out lúc đóng cửa)</div>
                    </div>
                  </div>
                ) : (
                  <div className="row muted"><QrCode size={18} /> Hiện bạn không ở phòng tập.</div>
                )}
                {apiActiveGym && (
                  <div className="small muted mt16">
                    <MapPin size={13} /> {apiActiveGym.planName} · {apiActiveGym.isAllBranches ? 'Tất cả chi nhánh' : apiActiveGym.branchName || 'Theo chi nhánh'}
                  </div>
                )}
              </>
            ) : (
              <>
                {open ? (
                  <div className="row"><span className="live-dot" /><div><b>Đang tập tại {L.branchById(db, open.branchId)?.name}</b><div className="small muted">Check-in lúc {fmtTime(open.checkInAt)} · nhớ check-out khi ra về (tự động check-out lúc đóng cửa)</div></div></div>
                ) : (
                  <div className="row muted"><QrCode size={18} /> Hiện bạn không ở phòng tập.</div>
                )}
                {plan && <div className="small muted mt16"><MapPin size={13} /> Áp dụng: {L.scopeLabel(db, plan.branchScope)}</div>}
              </>
            )}
          </div>
        </div>
        <div className="card">
          <div className="card-head"><h2><Clock size={18} /> Lịch sử ra/vào</h2></div>
          {apiMode ? (
            apiHistory.length === 0 ? <Empty text="Chưa có lượt check-in" /> : (
              <div className="col">
                {apiHistory.slice(0, 12).map((c) => (
                  <div className="list-item" key={c.id}>
                    <div className="grow">
                      <div className="bold">{c.branchName}</div>
                      <div className="small muted">
                        {fmtDateTime(c.checkInAt)}
                        {c.checkOutAt && ` → ${fmtTime(c.checkOutAt)}`}
                        {c.method.toLowerCase() === 'manual' && ' · Lễ tân nhập tay'}
                        {c.isAutoCheckout && ' · Tự động check-out'}
                      </div>
                      {c.attendedClassName && <div className="small" style={{ color: 'var(--primary)' }}>Điểm danh: {c.attendedClassName}</div>}
                    </div>
                    <Badge color="green">Hợp lệ</Badge>
                  </div>
                ))}
              </div>
            )
          ) : history.length === 0 ? <Empty text="Chưa có lượt check-in" /> : (
            <div className="col">
              {history.map((c) => (
                <div className="list-item" key={c.id}>
                  <div className="grow">
                    <div className="bold">{L.branchById(db, c.branchId)?.name}</div>
                    <div className="small muted">{fmtDateTime(c.checkInAt)}{c.checkOutAt && ` → ${fmtTime(c.checkOutAt)}`}{c.method === 'manual' && ' · Lễ tân nhập tay'}</div>
                    {c.denyReason && <div className="small" style={{ color: '#ff8aa0' }}>{c.denyReason}</div>}
                  </div>
                  <Badge color={c.result === 'success' ? 'green' : 'red'}>{c.result === 'success' ? 'Hợp lệ' : 'Từ chối'}</Badge>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </>
  );
}

/* ================= Giao dịch ================= */
export function MemberPayments() {
  const { db, user, toast } = useApp();
  const [apiMode, setApiMode] = useState<boolean>(!!getAuthToken());
  const [apiPayments, setApiPayments] = useState<ApiPaymentDto[]>([]);
  const [loading, setLoading] = useState<boolean>(false);

  useEffect(() => {
    if (!getAuthToken()) {
      setApiMode(false);
      return;
    }
    setLoading(true);
    membershipPlansApi
      .getMyPayments()
      .then((list) => {
        setApiPayments(list);
        setApiMode(true);
      })
      .catch((err) => {
        if (isApiNetworkError(err)) {
          setApiMode(false);
        } else {
          toast((err as Error).message, 'error');
        }
      })
      .finally(() => setLoading(false));
  }, [toast]);

  if (apiMode) {
    const total = apiPayments
      .filter((p) => p.status.toLowerCase() === 'completed')
      .reduce((s, p) => s + p.amount, 0);

    return (
      <>
        <PageHead title="Lịch sử giao dịch" desc={`${apiPayments.length} giao dịch · Tổng đã thanh toán ${fmtMoney(total)}`} />
        {loading ? (
          <div className="card" style={{ padding: 24 }}>Đang tải lịch sử giao dịch...</div>
        ) : apiPayments.length === 0 ? (
          <div className="card"><Empty text="Chưa có giao dịch" icon={<Receipt size={40} />} /></div>
        ) : (
          <div className="table-wrap">
            <table>
              <thead><tr><th>Mã GD</th><th>Thời gian</th><th>Nội dung</th><th>Chi nhánh</th><th>Phương thức</th><th>Số tiền</th><th>Trạng thái</th></tr></thead>
              <tbody>
                {apiPayments.map((p) => {
                  const st = p.status.toLowerCase();
                  const color = st === 'completed' ? 'green' : st === 'pending' ? 'amber' : 'red';
                  const label = st === 'completed' ? 'Thành công' : st === 'pending' ? 'Chờ xác nhận' : 'Thất bại';
                  return (
                    <tr key={p.id}>
                      <td className="small dim" style={{ fontFamily: 'monospace' }}>{(p.gatewayTxnCode || p.id).toUpperCase().slice(0, 22)}</td>
                      <td className="small">{fmtDateTime(p.paidAt || p.createdAt)}</td>
                      <td>{p.description}</td>
                      <td className="small">{p.branchName || 'Toàn hệ thống'}</td>
                      <td className="small">{formatPaymentMethod(p.method)}</td>
                      <td className="bold">{fmtMoney(p.amount)}</td>
                      <td><Badge color={color}>{label}</Badge></td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </>
    );
  }

  const list = db.payments.filter((p) => p.memberId === user!.id);
  const total = list.filter((p) => p.status === 'success').reduce((s, p) => s + p.amount, 0);
  return (
    <>
      <PageHead title="Lịch sử giao dịch" desc={`${list.length} giao dịch · Tổng đã thanh toán ${fmtMoney(total)}`} />
      {list.length === 0 ? <div className="card"><Empty text="Chưa có giao dịch" icon={<Receipt size={40} />} /></div> : (
        <div className="table-wrap">
          <table>
            <thead><tr><th>Mã GD</th><th>Thời gian</th><th>Nội dung</th><th>Chi nhánh</th><th>Phương thức</th><th>Số tiền</th><th>Trạng thái</th></tr></thead>
            <tbody>
              {list.map((p) => (
                <tr key={p.id}>
                  <td className="small dim" style={{ fontFamily: 'monospace' }}>{p.id.toUpperCase().slice(0, 12)}</td>
                  <td className="small">{fmtDateTime(p.createdAt)}</td>
                  <td>{p.description}</td>
                  <td className="small">{L.branchById(db, p.branchId)?.name}</td>
                  <td className="small">{formatPaymentMethod(p.method)}</td>
                  <td className="bold">{fmtMoney(p.amount)}</td>
                  <td><Badge color={p.status === 'success' ? 'green' : 'red'}>{p.status === 'success' ? 'Thành công' : 'Thất bại'}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

/* ================= Báo thiết bị hỏng ================= */
export function MemberEquipment() {
  const { db, user } = useApp();
  const [tab, setTab] = useState<'mine' | 'new'>('mine');
  const [branches, setBranches] = useState<{ id: string; name: string }[]>([]);
  const [equipments, setEquipments] = useState<Equipment[]>([]);
  const [reports, setReports] = useState<EquipmentReport[]>([]);
  const [branch, setBranch] = useState('');
  const [eq, setEq] = useState('');
  const [desc, setDesc] = useState('');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [err, setErr] = useState('');
  const [msg, setMsg] = useState('');

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
      setEquipments(eqList);
      setReports(repList);
      setBranch((prev) => prev || brList[0]?.id || '');
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Không thể tải dữ liệu thiết bị');
    } finally {
      setLoading(false);
    }
  }, [db.branches]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const mine = useMemo(
    () => reports.filter((r) => !r.reporterId || r.reporterId === user?.id),
    [reports, user?.id]
  );

  const eqs = useMemo(
    () => equipments.filter((e) => (!branch || e.branchId === branch) && e.status !== 'retired'),
    [equipments, branch]
  );

  const getEqText = (status: string) =>
    (eqLabel as Record<string, [string, string]>)[status]?.[0] ?? status;

  const getReportInfo = (status: string): [string, 'green' | 'amber' | 'red' | 'blue' | 'purple' | 'slate'] =>
    ((reportLabel as Record<string, [string, 'green' | 'amber' | 'red' | 'blue' | 'purple' | 'slate']>)[status] ?? [status, 'slate']);

  const branchName = (id?: string) =>
    branches.find((b) => b.id === id)?.name ?? L.branchById(db, id ?? '')?.name ?? '';

  const submit = async () => {
    setErr('');
    setMsg('');
    if (!eq) return setErr('Vui lòng chọn thiết bị');
    if (!desc.trim()) return setErr('Vui lòng mô tả sự cố');

    setSubmitting(true);
    try {
      await equipmentsApi.createReport({
        equipmentId: eq,
        description: desc.trim(),
      });
      setDesc('');
      setEq('');
      setMsg('Đã gửi phản ánh thành công, cảm ơn bạn!');
      setTab('mine');
      await loadData();
      emitAppDataChanged();
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Gửi phản ánh thất bại');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <PageHead title="Phản ánh thiết bị" desc="Phát hiện thiết bị hỏng hoặc sự cố? Hãy báo cho chúng tôi." />

      {err && <div className="card mb12" style={{ borderColor: 'var(--red)', color: 'var(--red)', padding: 12 }}>{err}</div>}
      {msg && <div className="card mb12" style={{ borderColor: 'var(--green)', color: 'var(--green)', padding: 12 }}>{msg}</div>}

      <Tabs
        value={tab}
        onChange={setTab}
        tabs={[
          { id: 'mine', label: 'Phản ánh của tôi', count: mine.filter((r) => r.status !== 'resolved').length },
          { id: 'new', label: <><Wrench size={15} /> Gửi phản ánh mới</> },
        ]}
      />

      {loading ? (
        <div className="card" style={{ padding: 24 }}>Đang tải dữ liệu thiết bị...</div>
      ) : tab === 'new' ? (
        <div className="card" style={{ maxWidth: 720 }}>
          <div className="form-grid">
            <div className="field">
              <label>Chi nhánh</label>
              <select className="input" value={branch} onChange={(e) => { setBranch(e.target.value); setEq(''); }}>
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>{b.name}</option>
                ))}
              </select>
            </div>
            <div className="field">
              <label>Thiết bị</label>
              <select className="input" value={eq} onChange={(e) => setEq(e.target.value)} id="eq-select">
                <option value="">— Chọn thiết bị —</option>
                {eqs.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.code ? `${e.code} · ` : ''}{e.name} ({getEqText(e.status)})
                  </option>
                ))}
              </select>
            </div>
            <div className="field full">
              <label>Mô tả sự cố</label>
              <textarea
                className="input"
                value={desc}
                onChange={(e) => setDesc(e.target.value)}
                placeholder="VD: Băng chạy bị trượt, phát tiếng kêu..."
                id="eq-desc"
              />
            </div>
          </div>
          <div className="row mt16" style={{ justifyContent: 'flex-end' }}>
            <button className="btn primary" onClick={submit} disabled={submitting} id="eq-submit">
              {submitting ? 'Đang gửi...' : 'Gửi phản ánh'}
            </button>
          </div>
        </div>
      ) : mine.length === 0 ? (
        <div className="card"><Empty text="Bạn chưa gửi phản ánh nào" /></div>
      ) : (
        <div className="grid g2">
          {mine.map((r) => {
            const e = equipments.find((x) => x.id === r.equipmentId);
            const [badgeText, badgeColor] = getReportInfo(r.status);
            return (
              <div className="card" key={r.id}>
                <div className="row between">
                  <div>
                    <b>{e?.name ?? 'Thiết bị'}</b>
                    <div className="small dim">
                      {e?.code ? `${e.code} · ` : ''}{branchName(r.branchId ?? e?.branchId)}
                    </div>
                  </div>
                  <Badge color={badgeColor}>{badgeText}</Badge>
                </div>
                <p className="muted mt8">{r.description}</p>
                {r.updates && r.updates.length > 0 && (
                  <>
                    <div className="divider" />
                    <div className="col" style={{ gap: 8 }}>
                      {r.updates.map((u, i) => {
                        const [uText] = getReportInfo(u.status);
                        return (
                          <div key={i} className="row small" style={{ alignItems: 'flex-start' }}>
                            <span style={{ width: 8, height: 8, borderRadius: 4, marginTop: 6, background: 'var(--primary-2)', flexShrink: 0 }} />
                            <div>
                              <b>{uText}</b> · <span className="dim">{fmtDateTime(u.at)} {u.by ? `· ${L.userById(db, u.by)?.fullName ?? u.by}` : ''}</span>
                              <div className="muted">{u.note}</div>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </>
                )}
              </div>
            );
          })}
        </div>
      )}
    </>
  );
}


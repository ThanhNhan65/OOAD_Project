import { useState, useEffect, useCallback } from 'react';
import { branchesApi, type BranchApiDto } from '../../api/branchesApi';
import { equipmentsApi } from '../../api/equipmentsApi';
import { membershipPlansApi, type ApiMembershipPlanDto } from '../../api/membershipPlansApi';
import { userService } from '../../api/user.service';
import { classesApi } from '../../api/classesApi';
import { getAuthToken, isApiNetworkError } from '../../api/apiClient';
import {
  Building2, Package, CalendarDays, Plus,
  Edit2, Calendar, Clock, MapPin, Sparkles, RefreshCw, Trash2
} from 'lucide-react';
import { useApp } from '../../store/AppContext';
import * as L from '../../store/logic';
import { Badge, Empty, Modal, PageHead, Tabs, ConfirmModal } from '../../components/ui';
import { fmtMoney, uid } from '../../utils';
import type { MembershipPlan, PTPlan } from '../../types';

export { AdminUsersScreen as AdminUsers } from './screens/AdminUsersScreen';
/* ========================================================================== */
/*  2. Quản lý Chi nhánh (AdminBranches)                                      */
/* ========================================================================== */
export function AdminBranches() {
  const [branches, setBranches] = useState<BranchApiDto[]>([]);
  const [eqCounts, setEqCounts] = useState<Record<string, number>>({});
  const [staffCounts, setStaffCounts] = useState<Record<string, number>>({});
  const [classCounts, setClassCounts] = useState<Record<string, number>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [err, setErr] = useState('');

  const [modalOpen, setModalOpen] = useState(false);
  const [editingBranch, setEditingBranch] = useState<BranchApiDto | null>(null);

  const [name, setName] = useState('');
  const [district, setDistrict] = useState('');
  const [address, setAddress] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [openTime, setOpenTime] = useState('05:30');
  const [closeTime, setCloseTime] = useState('22:00');
  const [isActive, setIsActive] = useState(true);

  const loadData = useCallback(async () => {
    setLoading(true);
    setErr('');
    try {
      const [brList, eqList, userList, clsList] = await Promise.all([
        branchesApi.getAll(),
        equipmentsApi.getAll().catch(() => []),
        userService.getAllUsers().catch(() => []),
        classesApi.getAll().catch(() => []),
      ]);
      setBranches(brList);

      const eqMap: Record<string, number> = {};
      for (const e of eqList) {
        eqMap[e.branchId] = (eqMap[e.branchId] || 0) + 1;
      }
      setEqCounts(eqMap);

      const stMap: Record<string, number> = {};
      for (const u of userList) {
        if (u.branchId && u.role !== 'member') {
          stMap[u.branchId] = (stMap[u.branchId] || 0) + 1;
        }
      }
      setStaffCounts(stMap);

      const clMap: Record<string, number> = {};
      for (const c of clsList) {
        if (c.active) {
          clMap[c.branchId] = (clMap[c.branchId] || 0) + 1;
        }
      }
      setClassCounts(clMap);
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Không thể tải danh sách chi nhánh');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const parseHours = (hours: string): [string, string] => {
    const parts = hours.split(/[-–]/).map((s) => s.trim());
    return [parts[0] || '05:30', parts[1] || '22:00'];
  };

  const openEdit = (b: BranchApiDto) => {
    const [o, c] = parseHours(b.operatingHours);
    setEditingBranch(b);
    setName(b.name);
    setDistrict(b.district);
    setAddress(b.address);
    setPhoneNumber(b.phoneNumber);
    setOpenTime(o);
    setCloseTime(c);
    setIsActive(b.isActive);
    setErr('');
    setModalOpen(true);
  };

  const openCreate = () => {
    setEditingBranch(null);
    setName('');
    setDistrict('');
    setAddress('');
    setPhoneNumber('');
    setOpenTime('05:30');
    setCloseTime('22:00');
    setIsActive(true);
    setErr('');
    setModalOpen(true);
  };

  const handleSave = async () => {
    if (!name.trim()) return setErr('Vui lòng nhập tên chi nhánh');
    if (!district.trim()) return setErr('Vui lòng nhập quận / khu vực');
    if (!address.trim()) return setErr('Vui lòng nhập địa chỉ');

    setSaving(true);
    setErr('');
    const operatingHours = `${openTime} - ${closeTime}`;
    try {
      if (editingBranch) {
        await branchesApi.update(editingBranch.id, {
          name: name.trim(),
          district: district.trim(),
          address: address.trim(),
          phoneNumber: phoneNumber.trim(),
          operatingHours,
          isActive,
        });
      } else {
        await branchesApi.create({
          name: name.trim(),
          district: district.trim(),
          address: address.trim(),
          phoneNumber: phoneNumber.trim(),
          operatingHours,
        });
      }
      setModalOpen(false);
      await loadData();
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Lưu chi nhánh thất bại');
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <PageHead
        title="Quản lý Chi nhánh"
        desc="Cấu hình danh sách cơ sở, địa chỉ và thời gian hoạt động."
        actions={
          <button className="btn primary" onClick={openCreate} id="btn-add-branch">
            <Plus size={16} /> Thêm chi nhánh
          </button>
        }
      />

      {err && !modalOpen && (
        <div className="card mb16" style={{ borderColor: 'var(--red)', color: 'var(--red)', padding: 12 }}>
          {err}
        </div>
      )}

      {loading ? (
        <div className="card" style={{ padding: 24 }}>Đang tải danh sách chi nhánh...</div>
      ) : branches.length === 0 ? (
        <div className="card"><Empty text="Chưa có chi nhánh nào" /></div>
      ) : (
        <div className="grid g3">
          {branches.map((b) => {
            const staffCount = staffCounts[b.id] ?? 0;
            const eqCount = eqCounts[b.id] ?? 0;
            const classCount = classCounts[b.id] ?? 0;
            return (
              <div className="card hover" key={b.id} style={{ opacity: b.isActive ? 1 : 0.65 }}>
                <div className="card-head">
                  <div className="row">
                    <Building2 size={20} color="#7c5cff" />
                    <div>
                      <h2>{b.name}</h2>
                      <div className="small dim">{b.district}</div>
                    </div>
                  </div>
                  <div className="row" style={{ gap: 6 }}>
                    <Badge color={b.isActive ? 'green' : 'gray'}>{b.isActive ? 'Hoạt động' : 'Tạm đóng'}</Badge>
                    <button className="btn sm ghost" onClick={() => openEdit(b)} id={`edit-branch-${b.id}`}>
                      <Edit2 size={14} /> Sửa
                    </button>
                  </div>
                </div>

                <div className="col small mb16" style={{ gap: 6 }}>
                  <div className="row muted"><MapPin size={14} /> {b.address}</div>
                  <div className="row muted"><Clock size={14} /> Giờ mở cửa: {b.operatingHours}</div>
                  {b.phoneNumber && <div className="row muted">📞 {b.phoneNumber}</div>}
                </div>

                <div className="grid g3 pt16" style={{ borderTop: '1px solid var(--border)', textAlign: 'center', paddingTop: 10 }}>
                  <div>
                    <div className="bold">{staffCount}</div>
                    <div className="small dim">Nhân viên</div>
                  </div>
                  <div>
                    <div className="bold">{classCount}</div>
                    <div className="small dim">Lớp học</div>
                  </div>
                  <div>
                    <div className="bold">{eqCount}</div>
                    <div className="small dim">Thiết bị</div>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {modalOpen && (
        <Modal
          title={editingBranch ? 'Chỉnh sửa chi nhánh' : 'Thêm chi nhánh mới'}
          onClose={() => setModalOpen(false)}
          footer={
            <>
              <button className="btn" onClick={() => setModalOpen(false)}>Hủy</button>
              <button className="btn primary" disabled={saving} onClick={handleSave} id="save-branch">
                {saving ? 'Đang lưu...' : 'Lưu'}
              </button>
            </>
          }
        >
          {err && <div className="small mb12" style={{ color: 'var(--red)' }}>{err}</div>}
          <div className="form-grid">
            <div className="field">
              <label>Tên chi nhánh *</label>
              <input className="input" value={name} onChange={(e) => setName(e.target.value)} placeholder="VD: OOAD Quận 1" id="branch-name" />
            </div>
            <div className="field">
              <label>Quận / Khu vực *</label>
              <input className="input" value={district} onChange={(e) => setDistrict(e.target.value)} placeholder="VD: Quận 1" id="branch-district" />
            </div>
            <div className="field full">
              <label>Địa chỉ *</label>
              <input className="input" value={address} onChange={(e) => setAddress(e.target.value)} placeholder="VD: 123 Nguyễn Huệ, Quận 1, TP.HCM" id="branch-address" />
            </div>
            <div className="field">
              <label>Số điện thoại</label>
              <input className="input" value={phoneNumber} onChange={(e) => setPhoneNumber(e.target.value)} placeholder="028 3888 1111" />
            </div>
            <div className="field">
              <label>Giờ hoạt động (Mở - Đóng)</label>
              <div className="row" style={{ gap: 6 }}>
                <input type="time" className="input" value={openTime} onChange={(e) => setOpenTime(e.target.value)} />
                <span>–</span>
                <input type="time" className="input" value={closeTime} onChange={(e) => setCloseTime(e.target.value)} />
              </div>
            </div>
            {editingBranch && (
              <div className="field full">
                <label className="check">
                  <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
                  Chi nhánh đang hoạt động
                </label>
              </div>
            )}
          </div>
        </Modal>
      )}
    </>
  );
}

/* ========================================================================== */
/*  3. Quản lý Danh mục Gói tập (AdminPlans)                                  */
/* ========================================================================== */
const planPriorityColor = (priorityLevel: number) =>
  priorityLevel >= 3 ? '#ff5c8a' : priorityLevel === 2 ? '#7c5cff' : '#3fa9ff';

export function AdminPlans() {
  const { db, act, toast } = useApp();
  const [tab, setTab] = useState<'membership' | 'pt'>('membership');
  const [apiMode, setApiMode] = useState<boolean>(true);
  const [loading, setLoading] = useState<boolean>(false);
  const [apiPlans, setApiPlans] = useState<ApiMembershipPlanDto[]>([]);
  const [apiBranches, setApiBranches] = useState<BranchApiDto[]>([]);
  const [msModal, setMsModal] = useState<ApiMembershipPlanDto | MembershipPlan | 'new' | null>(null);
  const [ptModal, setPtModal] = useState<ApiMembershipPlanDto | PTPlan | 'new' | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);

  const loadPlans = useCallback(async () => {
    setLoading(true);
    try {
      const [planList, brList] = await Promise.all([
        membershipPlansApi.getAll(),
        branchesApi.getAll(),
      ]);
      setApiPlans(planList);
      setApiBranches(brList);
      setApiMode(true);
    } catch (err) {
      if (isApiNetworkError(err)) {
        setApiMode(false);
      } else {
        toast((err as Error).message, 'error');
      }
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    loadPlans();
  }, [loadPlans]);

  const apiMsPlans = apiPlans.filter((p) => p.planType.toUpperCase() !== 'PT');
  const apiPtPlans = apiPlans.filter((p) => p.planType.toUpperCase() === 'PT');

  const branchOptions = apiMode
    ? apiBranches.map((b) => ({ id: b.id, name: b.name }))
    : db.branches.map((b) => ({ id: b.id, name: b.name }));

  const toggleMsActive = (p: MembershipPlan) => {
    act((d) => {
      const target = d.membershipPlans.find((x) => x.id === p.id);
      if (target) target.active = !target.active;
    }, p.active ? 'Đã vô hiệu gói' : 'Đã kích hoạt gói');
  };

  const togglePtActive = (p: PTPlan) => {
    act((d) => {
      const target = d.ptPlans.find((x) => x.id === p.id);
      if (target) target.active = !target.active;
    }, p.active ? 'Đã vô hiệu gói PT' : 'Đã kích hoạt gói PT');
  };

  const confirmDeleteApiPlan = async () => {
    if (!deleteTarget) return;
    const target = deleteTarget;
    setDeleteTarget(null);
    try {
      await membershipPlansApi.remove(target.id);
      toast(`Đã xóa gói "${target.name}"`, 'success');
      await loadPlans();
    } catch (err) {
      toast((err as Error).message, 'error');
    }
  };

  return (
    <>
      <PageHead
        title="Quản lý Danh mục Gói tập"
        desc="Cấu hình các gói hội viên và gói PT 1-kèm-1, phạm vi chi nhánh, số buổi PT tặng kèm và chính sách tạm ngưng (freeze)."
        actions={
          <div className="row" style={{ gap: 8 }}>
            {apiMode && (
              <button className="btn sm" onClick={loadPlans} disabled={loading}>
                <RefreshCw size={14} className={loading ? 'spin' : ''} /> Làm mới
              </button>
            )}
            <button
              className="btn primary"
              onClick={() => (tab === 'membership' ? setMsModal('new') : setPtModal('new'))}
              id="btn-add-plan"
            >
              <Plus size={16} /> {tab === 'membership' ? 'Thêm gói hội viên' : 'Thêm gói PT'}
            </button>
          </div>
        }
      />

      <Tabs
        value={tab}
        onChange={setTab}
        tabs={[
          {
            id: 'membership',
            label: <><Package size={15} /> Gói Hội Viên</>,
            count: apiMode ? apiMsPlans.length : db.membershipPlans.length,
          },
          {
            id: 'pt',
            label: <><Sparkles size={15} /> Gói Huấn Luyện Viên (PT)</>,
            count: apiMode ? apiPtPlans.length : db.ptPlans.length,
          },
        ]}
      />

      {apiMode ? (
        loading && apiPlans.length === 0 ? (
          <div className="card" style={{ padding: 24 }}>Đang tải danh mục gói tập...</div>
        ) : tab === 'membership' ? (
          apiMsPlans.length === 0 ? (
            <div className="card"><Empty text="Chưa có gói hội viên nào" /></div>
          ) : (
            <div className="grid g-auto">
              {apiMsPlans.map((p) => {
                const color = planPriorityColor(p.priorityLevel);
                const scopeText = p.isAllBranches
                  ? 'Tất cả chi nhánh'
                  : p.branchNames.length > 0
                    ? p.branchNames.join(', ')
                    : 'Theo chi nhánh đã chọn';
                return (
                  <div className="card plan-card hover" key={p.id}>
                    <div className="plan-top" style={{ background: color }} />
                    <div className="row between">
                      <h2>{p.name}</h2>
                      <Badge color="green">Đang áp dụng</Badge>
                    </div>
                    <div className="price">{fmtMoney(p.price)}</div>
                    <div className="small dim">
                      Thời hạn: {p.durationDays} ngày (~{p.durationMonths} tháng) · Ưu tiên waitlist: Cấp {p.priorityLevel}
                    </div>

                    <ul>
                      <li><MapPin size={14} /> Phạm vi: {scopeText}</li>
                      <li><Sparkles size={14} /> Buổi PT tặng kèm: {p.ptSessions || 'Không'}</li>
                      <li>
                        <CalendarDays size={14} /> Freeze:{' '}
                        {p.maxFreezeTimes ? `${p.maxFreezeTimes} lần (tối đa ${p.maxFreezeDays} ngày)` : 'Không cho phép'}
                      </li>
                      <li>
                        <Calendar size={14} /> Lớp nhóm:{' '}
                        {p.maxBookingsPerMonth ? `${p.maxBookingsPerMonth} lượt/tháng` : 'Không giới hạn'}
                      </li>
                    </ul>

                    <div className="row mt16" style={{ marginTop: 'auto', gap: 8 }}>
                      <button className="btn sm ghost grow" onClick={() => setMsModal(p)} id={`edit-plan-${p.id}`}>
                        <Edit2 size={14} /> Chỉnh sửa
                      </button>
                      <button
                        className="btn sm danger"
                        onClick={() => setDeleteTarget({ id: p.id, name: p.name })}
                        title="Xóa gói (chỉ xóa khi chưa có hội viên đăng ký)"
                      >
                        <Trash2 size={14} /> Xóa
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          )
        ) : apiPtPlans.length === 0 ? (
          <div className="card"><Empty text="Chưa có gói PT nào" /></div>
        ) : (
          <div className="grid g-auto">
            {apiPtPlans.map((p) => {
              const scopeText = p.isAllBranches
                ? 'Tất cả chi nhánh'
                : p.branchNames.length > 0
                  ? p.branchNames.join(', ')
                  : 'Theo chi nhánh đã chọn';
              return (
                <div className="card plan-card hover" key={p.id}>
                  <div className="plan-top" style={{ background: 'var(--grad-teal)' }} />
                  <div className="row between">
                    <h2>{p.name}</h2>
                    <Badge color="green">Đang áp dụng</Badge>
                  </div>
                  <div className="price">{fmtMoney(p.price)}</div>
                  <div className="small dim">
                    ≈ {fmtMoney(Math.round(p.price / Math.max(1, p.ptSessions)))}/buổi
                  </div>

                  <ul>
                    <li><Sparkles size={14} /> Số buổi: {p.ptSessions} buổi tập 1-kèm-1</li>
                    <li><Clock size={14} /> Thời hạn sử dụng: {p.durationDays} ngày</li>
                    <li><MapPin size={14} /> Phạm vi: {scopeText}</li>
                  </ul>

                  <div className="row mt16" style={{ marginTop: 'auto', gap: 8 }}>
                    <button className="btn sm ghost grow" onClick={() => setPtModal(p)} id={`edit-pt-plan-${p.id}`}>
                      <Edit2 size={14} /> Chỉnh sửa
                    </button>
                    <button
                      className="btn sm danger"
                      onClick={() => setDeleteTarget({ id: p.id, name: p.name })}
                    >
                      <Trash2 size={14} /> Xóa
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )
      ) : tab === 'membership' ? (
        <div className="grid g-auto">
          {db.membershipPlans.map((p) => (
            <div className="card plan-card hover" key={p.id} style={{ opacity: p.active ? 1 : 0.65 }}>
              <div className="plan-top" style={{ background: p.color }} />
              <div className="row between">
                <h2>{p.name}</h2>
                <Badge color={p.active ? 'green' : 'gray'}>{p.active ? 'Hoạt động' : 'Tạm dừng'}</Badge>
              </div>
              <div className="small muted mt8">{p.description}</div>
              <div className="price">{fmtMoney(p.price)}</div>
              <div className="small dim">Thời hạn: {p.durationMonths} tháng · Ưu tiên waitlist: Cấp {p.priority}</div>

              <ul>
                <li><MapPin size={14} /> Phạm vi: {L.scopeLabel(db, p.branchScope)}</li>
                <li><Sparkles size={14} /> Buổi PT tặng kèm: {p.freePTSessions || 'Không'}</li>
                <li><CalendarDays size={14} /> Freeze: {p.maxFreezeTimes ? `${p.maxFreezeTimes} lần (tối đa ${p.maxFreezeDays} ngày)` : 'Không cho phép'}</li>
                <li><Calendar size={14} /> Lớp nhóm: {p.classBookingsPerWeek ? `${p.classBookingsPerWeek} lượt/tuần` : 'Không giới hạn'}</li>
              </ul>

              <div className="row mt16" style={{ marginTop: 'auto', gap: 8 }}>
                <button className="btn sm ghost grow" onClick={() => setMsModal(p)} id={`edit-plan-${p.id}`}>
                  <Edit2 size={14} /> Chỉnh sửa
                </button>
                <button
                  className={`btn sm ${p.active ? 'danger' : 'success'}`}
                  onClick={() => toggleMsActive(p)}
                >
                  {p.active ? 'Tạm dừng' : 'Kích hoạt'}
                </button>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="grid g-auto">
          {db.ptPlans.map((p) => (
            <div className="card plan-card hover" key={p.id} style={{ opacity: p.active ? 1 : 0.65 }}>
              <div className="plan-top" style={{ background: 'var(--grad-teal)' }} />
              <div className="row between">
                <h2>{p.name}</h2>
                <Badge color={p.active ? 'green' : 'gray'}>{p.active ? 'Hoạt động' : 'Tạm dừng'}</Badge>
              </div>
              <div className="small muted mt8">{p.description}</div>
              <div className="price">{fmtMoney(p.price)}</div>
              <div className="small dim">≈ {fmtMoney(Math.round(p.price / p.sessions))}/buổi</div>

              <ul>
                <li><Sparkles size={14} /> Số buổi: {p.sessions} buổi tập 1-kèm-1</li>
                <li><Clock size={14} /> Thời hạn sử dụng: {p.validityDays} ngày</li>
                <li><MapPin size={14} /> Phạm vi: {L.scopeLabel(db, p.branchScope)}</li>
              </ul>

              <div className="row mt16" style={{ marginTop: 'auto', gap: 8 }}>
                <button className="btn sm ghost grow" onClick={() => setPtModal(p)} id={`edit-pt-plan-${p.id}`}>
                  <Edit2 size={14} /> Chỉnh sửa
                </button>
                <button
                  className={`btn sm ${p.active ? 'danger' : 'success'}`}
                  onClick={() => togglePtActive(p)}
                >
                  {p.active ? 'Tạm dừng' : 'Kích hoạt'}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {msModal && (
        <MembershipPlanModal
          plan={msModal === 'new' ? null : msModal}
          apiMode={apiMode}
          branches={branchOptions}
          onSaved={loadPlans}
          onClose={() => setMsModal(null)}
        />
      )}

      {ptModal && (
        <PTPlanModal
          plan={ptModal === 'new' ? null : ptModal}
          apiMode={apiMode}
          branches={branchOptions}
          onSaved={loadPlans}
          onClose={() => setPtModal(null)}
        />
      )}

      {deleteTarget && (
        <ConfirmModal
          title="Xóa gói tập"
          danger
          confirmText="Xóa gói"
          message={<>Bạn có chắc muốn xóa gói <b>{deleteTarget.name}</b>? (Chỉ xóa được khi chưa có hội viên đăng ký.)</>}
          onClose={() => setDeleteTarget(null)}
          onConfirm={confirmDeleteApiPlan}
        />
      )}
    </>
  );
}

function isApiPlan(p: ApiMembershipPlanDto | MembershipPlan | PTPlan | null): p is ApiMembershipPlanDto {
  return !!p && 'durationDays' in p;
}

function MembershipPlanModal({
  plan,
  apiMode,
  branches,
  onSaved,
  onClose,
}: {
  plan: ApiMembershipPlanDto | MembershipPlan | null;
  apiMode: boolean;
  branches: { id: string; name: string }[];
  onSaved: () => Promise<void>;
  onClose: () => void;
}) {
  const { act, toast } = useApp();
  const apiPlan = isApiPlan(plan) ? plan : null;
  const localPlan = plan && !isApiPlan(plan) ? plan : null;

  const [name, setName] = useState(plan?.name || '');
  const [durationDays, setDurationDays] = useState(apiPlan ? apiPlan.durationDays : (localPlan?.durationMonths || 1) * 30);
  const [price, setPrice] = useState(plan?.price ?? 790000);
  const [priority, setPriority] = useState(apiPlan ? apiPlan.priorityLevel : localPlan?.priority ?? 1);
  const [freePTSessions, setFreePTSessions] = useState(apiPlan ? apiPlan.ptSessions : localPlan?.freePTSessions ?? 0);
  const [maxFreezeTimes, setMaxFreezeTimes] = useState(apiPlan ? apiPlan.maxFreezeTimes : localPlan?.maxFreezeTimes ?? 0);
  const [maxFreezeDays, setMaxFreezeDays] = useState(apiPlan ? apiPlan.maxFreezeDays : localPlan?.maxFreezeDays ?? 0);
  const [maxBookingsPerMonth, setMaxBookingsPerMonth] = useState(
    apiPlan ? apiPlan.maxBookingsPerMonth : (localPlan?.classBookingsPerWeek ?? 0) * 4,
  );
  const [description, setDescription] = useState(localPlan?.description || '');
  const [color, setColor] = useState(localPlan?.color || '#7c5cff');
  const [scopeAll, setScopeAll] = useState(
    apiPlan ? apiPlan.isAllBranches : localPlan ? localPlan.branchScope === 'all' : true,
  );
  const [selectedBranches, setSelectedBranches] = useState<string[]>(
    apiPlan && !apiPlan.isAllBranches && apiPlan.branchIds.length > 0
      ? apiPlan.branchIds
      : localPlan && Array.isArray(localPlan.branchScope)
        ? localPlan.branchScope
        : branches[0]?.id ? [branches[0].id] : [],
  );
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    if (!name.trim()) return toast('Vui lòng nhập tên gói', 'error');
    if (!scopeAll && selectedBranches.length === 0) return toast('Vui lòng chọn ít nhất 1 chi nhánh', 'error');

    if (apiMode && getAuthToken()) {
      setSaving(true);
      try {
        const payload = {
          name: name.trim(),
          durationDays: Math.max(1, durationDays),
          price: Math.max(0, price),
          priorityLevel: priority,
          maxBookingsPerMonth: Math.max(0, maxBookingsPerMonth),
          maxFreezeTimes: Math.max(0, maxFreezeTimes),
          maxFreezeDays: Math.max(0, maxFreezeDays),
          isAllBranches: scopeAll,
          planType: 'MEMBERSHIP' as const,
          ptSessions: Math.max(0, freePTSessions),
          branchIds: scopeAll ? [] : selectedBranches,
        };
        if (apiPlan) {
          await membershipPlansApi.update(apiPlan.id, payload);
          toast('Đã cập nhật gói hội viên', 'success');
        } else {
          await membershipPlansApi.create(payload);
          toast('Đã thêm gói hội viên mới', 'success');
        }
        await onSaved();
        onClose();
      } catch (err) {
        toast((err as Error).message, 'error');
      } finally {
        setSaving(false);
      }
      return;
    }

    act((d) => {
      const branchScope = scopeAll ? 'all' : selectedBranches;
      const durationMonths = Math.max(1, Math.round(durationDays / 30));
      const classBookingsPerWeek = maxBookingsPerMonth > 0 ? Math.max(1, Math.round(maxBookingsPerMonth / 4)) : 0;
      const data: MembershipPlan = {
        id: localPlan ? localPlan.id : uid('mp'),
        name: name.trim(),
        durationMonths,
        price,
        priority,
        freePTSessions,
        maxFreezeTimes,
        maxFreezeDays,
        classBookingsPerWeek,
        description,
        benefits: ['Tập luyện tại chi nhánh áp dụng', `${classBookingsPerWeek ? `${classBookingsPerWeek} lớp/tuần` : 'Lớp nhóm không giới hạn'}`],
        color,
        branchScope,
        active: localPlan ? localPlan.active : true,
      };

      if (localPlan) {
        const idx = d.membershipPlans.findIndex((x) => x.id === localPlan.id);
        if (idx >= 0) d.membershipPlans[idx] = data;
      } else {
        d.membershipPlans.push(data);
      }
    }, localPlan ? 'Đã cập nhật gói hội viên' : 'Đã thêm gói hội viên mới');
    onClose();
  };

  return (
    <Modal
      wide
      title={plan ? `Chỉnh sửa: ${plan.name}` : 'Thêm gói hội viên mới'}
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={saving}>Hủy</button>
          <button className="btn primary" onClick={handleSave} disabled={saving} id="save-ms-plan">
            {saving ? 'Đang lưu...' : 'Lưu gói tập'}
          </button>
        </>
      }
    >
      <div className="form-grid">
        <div className="field">
          <label>Tên gói *</label>
          <input className="input" value={name} onChange={(e) => setName(e.target.value)} placeholder="VD: Gói VIP 12 tháng" id="plan-name" />
        </div>
        <div className="field">
          <label>Giá (VNĐ) *</label>
          <input className="input" type="number" step="50000" value={price} onChange={(e) => setPrice(+e.target.value)} id="plan-price" />
        </div>
        <div className="field">
          <label>Thời hạn (ngày) *</label>
          <input className="input" type="number" min={1} value={durationDays} onChange={(e) => setDurationDays(+e.target.value)} />
        </div>
        <div className="field">
          <label>Cấp độ ưu tiên Waitlist (1: Thấp, 3: VIP) *</label>
          <select className="input" value={priority} onChange={(e) => setPriority(+e.target.value)}>
            <option value={1}>Cấp 1 (Gói Basic / Standard)</option>
            <option value={2}>Cấp 2 (Gói Tiêu chuẩn)</option>
            <option value={3}>Cấp 3 (Gói VIP - Ưu tiên hàng đầu)</option>
          </select>
        </div>
        <div className="field">
          <label>Số buổi PT tặng kèm (miễn phí)</label>
          <input className="input" type="number" min={0} value={freePTSessions} onChange={(e) => setFreePTSessions(+e.target.value)} />
        </div>
        <div className="field">
          <label>Giới hạn lớp học / tháng (0 = Không giới hạn)</label>
          <input className="input" type="number" min={0} value={maxBookingsPerMonth} onChange={(e) => setMaxBookingsPerMonth(+e.target.value)} />
        </div>
        <div className="field">
          <label>Số lần Freeze tối đa</label>
          <input className="input" type="number" min={0} value={maxFreezeTimes} onChange={(e) => setMaxFreezeTimes(+e.target.value)} />
        </div>
        <div className="field">
          <label>Tổng số ngày Freeze tối đa</label>
          <input className="input" type="number" min={0} value={maxFreezeDays} onChange={(e) => setMaxFreezeDays(+e.target.value)} />
        </div>
        {!apiMode && (
          <div className="field full">
            <label>Màu đại diện gói</label>
            <input type="color" value={color} onChange={(e) => setColor(e.target.value)} style={{ width: 80, height: 38, border: 'none', background: 'transparent', cursor: 'pointer' }} />
          </div>
        )}
        <div className="field full">
          <label>Phạm vi chi nhánh áp dụng</label>
          <div className="row wrap">
            <label className="check">
              <input type="checkbox" checked={scopeAll} onChange={(e) => setScopeAll(e.target.checked)} />
              Áp dụng cho tất cả chi nhánh
            </label>
          </div>
          {!scopeAll && (
            <div className="chips mt8">
              {branches.map((b) => {
                const sel = selectedBranches.includes(b.id);
                return (
                  <button
                    type="button"
                    key={b.id}
                    className={`chip ${sel ? 'on' : ''}`}
                    onClick={() => setSelectedBranches(sel ? selectedBranches.filter((x) => x !== b.id) : [...selectedBranches, b.id])}
                  >
                    {b.name}
                  </button>
                );
              })}
            </div>
          )}
        </div>
        {!apiMode && (
          <div className="field full">
            <label>Mô tả gói tập</label>
            <textarea className="input" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Tóm tắt đặc quyền của gói..." />
          </div>
        )}
      </div>
    </Modal>
  );
}

function PTPlanModal({
  plan,
  apiMode,
  branches,
  onSaved,
  onClose,
}: {
  plan: ApiMembershipPlanDto | PTPlan | null;
  apiMode: boolean;
  branches: { id: string; name: string }[];
  onSaved: () => Promise<void>;
  onClose: () => void;
}) {
  const { act, toast } = useApp();
  const apiPlan = isApiPlan(plan) ? plan : null;
  const localPlan = plan && !isApiPlan(plan) ? plan : null;

  const [name, setName] = useState(plan?.name || '');
  const [sessions, setSessions] = useState(apiPlan ? apiPlan.ptSessions : localPlan?.sessions ?? 10);
  const [validityDays, setValidityDays] = useState(apiPlan ? apiPlan.durationDays : localPlan?.validityDays ?? 60);
  const [price, setPrice] = useState(plan?.price ?? 3500000);
  const [description, setDescription] = useState(localPlan?.description || '');
  const [scopeAll, setScopeAll] = useState(
    apiPlan ? apiPlan.isAllBranches : localPlan ? localPlan.branchScope === 'all' : true,
  );
  const [selectedBranches, setSelectedBranches] = useState<string[]>(
    apiPlan && !apiPlan.isAllBranches && apiPlan.branchIds.length > 0
      ? apiPlan.branchIds
      : localPlan && Array.isArray(localPlan.branchScope)
        ? localPlan.branchScope
        : branches[0]?.id ? [branches[0].id] : [],
  );
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    if (!name.trim()) return toast('Vui lòng nhập tên gói PT', 'error');
    if (!scopeAll && selectedBranches.length === 0) return toast('Vui lòng chọn ít nhất 1 chi nhánh', 'error');

    if (apiMode && getAuthToken()) {
      setSaving(true);
      try {
        const payload = {
          name: name.trim(),
          durationDays: Math.max(1, validityDays),
          price: Math.max(0, price),
          priorityLevel: 1,
          maxBookingsPerMonth: 0,
          maxFreezeTimes: 0,
          maxFreezeDays: 0,
          isAllBranches: scopeAll,
          planType: 'PT' as const,
          ptSessions: Math.max(1, sessions),
          branchIds: scopeAll ? [] : selectedBranches,
        };
        if (apiPlan) {
          await membershipPlansApi.update(apiPlan.id, payload);
          toast('Đã cập nhật gói PT', 'success');
        } else {
          await membershipPlansApi.create(payload);
          toast('Đã thêm gói PT mới', 'success');
        }
        await onSaved();
        onClose();
      } catch (err) {
        toast((err as Error).message, 'error');
      } finally {
        setSaving(false);
      }
      return;
    }

    act((d) => {
      const branchScope = scopeAll ? 'all' : selectedBranches;
      const data: PTPlan = {
        id: localPlan ? localPlan.id : uid('ptp'),
        name: name.trim(),
        sessions,
        validityDays,
        price,
        branchScope,
        description,
        active: localPlan ? localPlan.active : true,
      };

      if (localPlan) {
        const idx = d.ptPlans.findIndex((x) => x.id === localPlan.id);
        if (idx >= 0) d.ptPlans[idx] = data;
      } else {
        d.ptPlans.push(data);
      }
    }, localPlan ? 'Đã cập nhật gói PT' : 'Đã thêm gói PT mới');
    onClose();
  };

  return (
    <Modal
      title={plan ? `Chỉnh sửa: ${plan.name}` : 'Thêm gói PT mới'}
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={saving}>Hủy</button>
          <button className="btn primary" onClick={handleSave} disabled={saving} id="save-pt-plan">
            {saving ? 'Đang lưu...' : 'Lưu gói PT'}
          </button>
        </>
      }
    >
      <div className="form-grid">
        <div className="field full">
          <label>Tên gói PT *</label>
          <input className="input" value={name} onChange={(e) => setName(e.target.value)} placeholder="VD: PT 10 buổi cá nhân" id="pt-plan-name" />
        </div>
        <div className="field">
          <label>Số buổi tập *</label>
          <input className="input" type="number" min={1} value={sessions} onChange={(e) => setSessions(+e.target.value)} />
        </div>
        <div className="field">
          <label>Thời hạn sử dụng (ngày) *</label>
          <input className="input" type="number" min={1} value={validityDays} onChange={(e) => setValidityDays(+e.target.value)} />
        </div>
        <div className="field full">
          <label>Giá (VNĐ) *</label>
          <input className="input" type="number" step="50000" value={price} onChange={(e) => setPrice(+e.target.value)} id="pt-plan-price" />
        </div>
        <div className="field full">
          <label>Phạm vi chi nhánh áp dụng</label>
          <div className="row wrap">
            <label className="check">
              <input type="checkbox" checked={scopeAll} onChange={(e) => setScopeAll(e.target.checked)} />
              Áp dụng toàn bộ chi nhánh
            </label>
          </div>
          {!scopeAll && (
            <div className="chips mt8">
              {branches.map((b) => {
                const sel = selectedBranches.includes(b.id);
                return (
                  <button
                    type="button"
                    key={b.id}
                    className={`chip ${sel ? 'on' : ''}`}
                    onClick={() => setSelectedBranches(sel ? selectedBranches.filter((x) => x !== b.id) : [...selectedBranches, b.id])}
                  >
                    {b.name}
                  </button>
                );
              })}
            </div>
          )}
        </div>
        {!apiMode && (
          <div className="field full">
            <label>Mô tả gói PT</label>
            <textarea className="input" value={description} onChange={(e) => setDescription(e.target.value)} />
          </div>
        )}
      </div>
    </Modal>
  );
}

/* ========================================================================== */
/*  4. Quản lý Lớp học Nhóm (AdminClasses)                                    */
/* ========================================================================== */
export { AdminClassesScreen as AdminClasses } from './screens/AdminClassesScreen';


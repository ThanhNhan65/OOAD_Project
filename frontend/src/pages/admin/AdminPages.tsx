import { useState, useEffect, useCallback } from 'react';
import { branchesApi, type BranchApiDto } from '../../api/branchesApi';
import { equipmentsApi } from '../../api/equipmentsApi';
import {
  UserPlus, Lock, Unlock, Building2, Package, CalendarDays, Plus,
  Edit2, Calendar, Clock, Save, MapPin, Sparkles
} from 'lucide-react';
import { useApp } from '../../store/AppContext';
import * as L from '../../store/logic';
import { Avatar, Badge, Empty, Modal, PageHead, Tabs } from '../../components/ui';
import { roleLabel } from '../../components/labels';
import { fmtDate, fmtMoney, uid } from '../../utils';
import type { User, Role, Branch, MembershipPlan, PTPlan, Settings } from '../../types';

/* ========================================================================== */
/*  1. Quản lý Tài khoản & Phân quyền (AdminUsers)                            */
/* ========================================================================== */
export function AdminUsers() {
  const { db, user: currentUser, act } = useApp();
  const [tab, setTab] = useState<Role | 'all'>('all');
  const [search, setSearch] = useState('');
  const [createModal, setCreateModal] = useState<boolean>(false);
  const [editingUser, setEditingUser] = useState<User | null>(null);

  const filteredUsers = db.users.filter((u) => {
    if (tab !== 'all' && u.role !== tab) return false;
    if (!search) return true;
    const term = search.toLowerCase();
    return (
      u.fullName.toLowerCase().includes(term) ||
      u.email.toLowerCase().includes(term) ||
      u.phone.includes(term) ||
      (u.cardCode && u.cardCode.toLowerCase().includes(term))
    );
  });

  const toggleLock = (targetUser: User) => {
    if (targetUser.id === currentUser?.id) {
      alert('Không thể tự khóa tài khoản của chính mình!');
      return;
    }
    act((d) => {
      const u = d.users.find((x) => x.id === targetUser.id);
      if (u) {
        u.locked = !u.locked;
        L.notify(
          d,
          u.id,
          u.locked ? 'Tài khoản đã bị khóa' : 'Tài khoản đã được mở khóa',
          u.locked
            ? 'Quản trị viên đã tạm khóa tài khoản của bạn.'
            : 'Tài khoản của bạn đã có thể hoạt động trở lại.',
          u.locked ? 'danger' : 'success'
        );
      }
    }, targetUser.locked ? 'Đã mở khóa tài khoản' : 'Đã khóa tài khoản');
  };

  return (
    <>
      <PageHead
        title="Quản lý Tài khoản & Phân quyền"
        desc="Quản trị người dùng toàn hệ thống, tạo tài khoản nhân viên/HLV và phân quyền chi nhánh."
        actions={
          <button className="btn primary" onClick={() => setCreateModal(true)} id="btn-add-staff">
            <UserPlus size={16} /> Thêm nhân viên / HLV
          </button>
        }
      />

      <div className="row wrap mb16" style={{ justifyContent: 'space-between', gap: 12 }}>
        <Tabs
          value={tab}
          onChange={setTab}
          tabs={[
            { id: 'all', label: 'Tất cả', count: db.users.length },
            { id: 'member', label: 'Hội viên', count: db.users.filter((u) => u.role === 'member').length },
            { id: 'trainer', label: 'Huấn luyện viên', count: db.users.filter((u) => u.role === 'trainer').length },
            { id: 'receptionist', label: 'Lễ tân', count: db.users.filter((u) => u.role === 'receptionist').length },
            { id: 'admin', label: 'Quản trị viên', count: db.users.filter((u) => u.role === 'admin').length },
          ]}
        />
        <div className="input-icon" style={{ minWidth: 260 }}>
          <input
            className="input"
            placeholder="Tìm theo tên, email, SĐT, mã thẻ..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            id="search-users"
          />
        </div>
      </div>

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Người dùng</th>
              <th>Vai trò</th>
              <th>Chi nhánh / Mã thẻ</th>
              <th>Số điện thoại</th>
              <th>Ngày tạo</th>
              <th>Trạng thái</th>
              <th style={{ textAlign: 'right' }}>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {filteredUsers.map((u) => {
              const [roleText, roleColor] = roleLabel[u.role];
              const branch = u.branchId ? L.branchById(db, u.branchId) : null;
              return (
                <tr key={u.id}>
                  <td>
                    <div className="row">
                      <Avatar user={u} size="sm" />
                      <div>
                        <b>{u.fullName}</b>
                        <div className="small dim">{u.email}</div>
                      </div>
                    </div>
                  </td>
                  <td>
                    <Badge color={roleColor}>{roleText}</Badge>
                  </td>
                  <td className="small">
                    {u.role === 'member' ? (
                      <span style={{ fontFamily: 'monospace' }}>{u.cardCode || '—'}</span>
                    ) : u.role === 'receptionist' ? (
                      branch ? branch.name : 'Chưa gán'
                    ) : u.role === 'trainer' ? (
                      u.trainer?.branchIds?.map((b) => L.branchById(db, b)?.name.replace('OOAD ', '')).join(', ') || 'Chưa gán'
                    ) : (
                      'Toàn hệ thống'
                    )}
                  </td>
                  <td className="small">{u.phone || '—'}</td>
                  <td className="small">{fmtDate(u.createdAt)}</td>
                  <td>
                    {u.locked ? (
                      <Badge color="red">Bị khóa</Badge>
                    ) : (
                      <Badge color="green">Hoạt động</Badge>
                    )}
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    <div className="row" style={{ justifyContent: 'flex-end', gap: 6 }}>
                      <button
                        className="btn sm ghost"
                        onClick={() => setEditingUser(u)}
                        title="Chỉnh sửa thông tin"
                        id={`edit-user-${u.id}`}
                      >
                        <Edit2 size={14} />
                      </button>
                      {u.id !== currentUser?.id && (
                        <button
                          className={`btn sm ${u.locked ? 'success' : 'danger'}`}
                          onClick={() => toggleLock(u)}
                          id={`lock-user-${u.id}`}
                        >
                          {u.locked ? <><Unlock size={14} /> Mở khóa</> : <><Lock size={14} /> Khóa</>}
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
        {filteredUsers.length === 0 && <Empty text="Không tìm thấy người dùng phù hợp" />}
      </div>

      {createModal && <CreateStaffModal onClose={() => setCreateModal(false)} />}
      {editingUser && <EditUserModal user={editingUser} onClose={() => setEditingUser(null)} />}
    </>
  );
}

function CreateStaffModal({ onClose }: { onClose: () => void }) {
  const { db, act } = useApp();
  const [role, setRole] = useState<'receptionist' | 'trainer' | 'admin'>('receptionist');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [branchId, setBranchId] = useState(db.branches[0]?.id || '');
  const [specialties, setSpecialties] = useState('Gym, HIIT, Cardio');
  const [experienceYears, setExperienceYears] = useState(3);

  const handleSave = () => {
    act((d) => {
      if (!fullName.trim() || !email.trim()) throw new Error('Vui lòng điền đủ họ tên và email');
      L.createStaff(d, {
        role,
        fullName: fullName.trim(),
        email: email.trim(),
        phone: phone.trim(),
        branchId: role === 'receptionist' ? branchId : undefined,
        trainer: role === 'trainer' ? {
          specialties: specialties.split(',').map((s) => s.trim()).filter(Boolean),
          experienceYears,
          certifications: ['Chứng chỉ thể hình Quốc gia'],
          bio: 'Huấn luyện viên chuyên môn cao tại OOAD',
          branchIds: [branchId],
        } : undefined,
      });
    }, 'Tạo tài khoản thành công! Mật khẩu mặc định là 123456');
    onClose();
  };

  return (
    <Modal
      title="Tạo tài khoản nhân viên / HLV"
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose}>Hủy</button>
          <button className="btn primary" onClick={handleSave} id="save-staff">Tạo tài khoản</button>
        </>
      }
    >
      <div className="form-grid">
        <div className="field full">
          <label>Vai trò</label>
          <div className="chips">
            <button className={`chip ${role === 'receptionist' ? 'on' : ''}`} onClick={() => setRole('receptionist')}>
              Lễ tân
            </button>
            <button className={`chip ${role === 'trainer' ? 'on' : ''}`} onClick={() => setRole('trainer')}>
              Huấn luyện viên (PT)
            </button>
            <button className={`chip ${role === 'admin' ? 'on' : ''}`} onClick={() => setRole('admin')}>
              Quản trị viên
            </button>
          </div>
        </div>

        <div className="field">
          <label>Họ và tên *</label>
          <input className="input" value={fullName} onChange={(e) => setFullName(e.target.value)} placeholder="VD: Lê Thị Mai" id="staff-name" />
        </div>
        <div className="field">
          <label>Email đăng nhập *</label>
          <input className="input" type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="mail@ooad.vn" id="staff-email" />
        </div>
        <div className="field">
          <label>Số điện thoại</label>
          <input className="input" value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="0901234567" />
        </div>

        {role !== 'admin' && (
          <div className="field">
            <label>{role === 'receptionist' ? 'Chi nhánh làm việc *' : 'Chi nhánh chính *'}</label>
            <select className="input" value={branchId} onChange={(e) => setBranchId(e.target.value)}>
              {db.branches.map((b) => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
          </div>
        )}

        {role === 'trainer' && (
          <>
            <div className="field full">
              <label>Chuyên môn (ngăn cách bằng dấu phẩy)</label>
              <input className="input" value={specialties} onChange={(e) => setSpecialties(e.target.value)} placeholder="Tăng cơ, Giảm mỡ, Yoga..." />
            </div>
            <div className="field">
              <label>Số năm kinh nghiệm</label>
              <input className="input" type="number" min={0} value={experienceYears} onChange={(e) => setExperienceYears(+e.target.value)} />
            </div>
          </>
        )}
      </div>
      <p className="small muted mt16">💡 Mật khẩu đăng nhập ban đầu mặc định là <b>123456</b>. Người dùng có thể tự đổi sau khi đăng nhập.</p>
    </Modal>
  );
}

function EditUserModal({ user, onClose }: { user: User; onClose: () => void }) {
  const { db, act } = useApp();
  const [fullName, setFullName] = useState(user.fullName);
  const [phone, setPhone] = useState(user.phone);
  const [branchId, setBranchId] = useState(user.branchId || db.branches[0]?.id || '');

  const handleUpdate = () => {
    act((d) => {
      const u = d.users.find((x) => x.id === user.id);
      if (u) {
        u.fullName = fullName.trim();
        u.phone = phone.trim();
        if (u.role === 'receptionist') u.branchId = branchId;
      }
    }, 'Cập nhật thông tin người dùng thành công');
    onClose();
  };

  return (
    <Modal
      title={`Chỉnh sửa: ${user.fullName}`}
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose}>Hủy</button>
          <button className="btn primary" onClick={handleUpdate} id="save-edit-user">Lưu thay đổi</button>
        </>
      }
    >
      <div className="form-grid">
        <div className="field">
          <label>Họ và tên</label>
          <input className="input" value={fullName} onChange={(e) => setFullName(e.target.value)} />
        </div>
        <div className="field">
          <label>Email</label>
          <input className="input" value={user.email} disabled />
        </div>
        <div className="field">
          <label>Số điện thoại</label>
          <input className="input" value={phone} onChange={(e) => setPhone(e.target.value)} />
        </div>
        {user.role === 'receptionist' && (
          <div className="field">
            <label>Chi nhánh làm việc</label>
            <select className="input" value={branchId} onChange={(e) => setBranchId(e.target.value)}>
              {db.branches.map((b) => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
          </div>
        )}
      </div>
    </Modal>
  );
}

/* ========================================================================== */
/*  2. Quản lý Chi nhánh (AdminBranches)                                      */
/* ========================================================================== */
export function AdminBranches() {
  const { db } = useApp();
  const [branches, setBranches] = useState<BranchApiDto[]>([]);
  const [eqCounts, setEqCounts] = useState<Record<string, number>>({});
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
      const [brList, eqList] = await Promise.all([
        branchesApi.getAll(),
        equipmentsApi.getAll().catch(() => []),
      ]);
      setBranches(brList);
      const counts: Record<string, number> = {};
      for (const e of eqList) {
        counts[e.branchId] = (counts[e.branchId] || 0) + 1;
      }
      setEqCounts(counts);
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
            const staffCount = db.users.filter((u) => u.branchId === b.id).length;
            const eqCount = eqCounts[b.id] ?? 0;
            const classCount = db.classes.filter((c) => c.branchId === b.id && c.active).length;
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
export function AdminPlans() {
  const { db, act } = useApp();
  const [tab, setTab] = useState<'membership' | 'pt'>('membership');
  const [msModal, setMsModal] = useState<MembershipPlan | 'new' | null>(null);
  const [ptModal, setPtModal] = useState<PTPlan | 'new' | null>(null);

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

  return (
    <>
      <PageHead
        title="Quản lý Danh mục Gói tập"
        desc="Cấu hình các gói hội viên và gói PT 1-kèm-1, phạm vi chi nhánh, số buổi PT tặng kèm và chính sách tạm ngưng (freeze)."
        actions={
          <button
            className="btn primary"
            onClick={() => (tab === 'membership' ? setMsModal('new') : setPtModal('new'))}
            id="btn-add-plan"
          >
            <Plus size={16} /> {tab === 'membership' ? 'Thêm gói hội viên' : 'Thêm gói PT'}
          </button>
        }
      />

      <Tabs
        value={tab}
        onChange={setTab}
        tabs={[
          { id: 'membership', label: <><Package size={15} /> Gói Hội Viên</>, count: db.membershipPlans.length },
          { id: 'pt', label: <><Sparkles size={15} /> Gói Huấn Luyện Viên (PT)</>, count: db.ptPlans.length },
        ]}
      />

      {tab === 'membership' ? (
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
          onClose={() => setMsModal(null)}
        />
      )}

      {ptModal && (
        <PTPlanModal
          plan={ptModal === 'new' ? null : ptModal}
          onClose={() => setPtModal(null)}
        />
      )}
    </>
  );
}

function MembershipPlanModal({ plan, onClose }: { plan: MembershipPlan | null; onClose: () => void }) {
  const { db, act } = useApp();
  const [name, setName] = useState(plan?.name || '');
  const [durationMonths, setDurationMonths] = useState(plan?.durationMonths || 1);
  const [price, setPrice] = useState(plan?.price || 500000);
  const [priority, setPriority] = useState(plan?.priority || 1);
  const [freePTSessions, setFreePTSessions] = useState(plan?.freePTSessions || 0);
  const [maxFreezeTimes, setMaxFreezeTimes] = useState(plan?.maxFreezeTimes || 0);
  const [maxFreezeDays, setMaxFreezeDays] = useState(plan?.maxFreezeDays || 0);
  const [classBookingsPerWeek, setClassBookingsPerWeek] = useState(plan?.classBookingsPerWeek || 0);
  const [description, setDescription] = useState(plan?.description || '');
  const [color, setColor] = useState(plan?.color || '#7c5cff');
  const [scopeAll, setScopeAll] = useState(plan ? plan.branchScope === 'all' : true);
  const [selectedBranches, setSelectedBranches] = useState<string[]>(
    plan && Array.isArray(plan.branchScope) ? plan.branchScope : [db.branches[0]?.id || '']
  );

  const handleSave = () => {
    act((d) => {
      if (!name.trim()) throw new Error('Vui lòng nhập tên gói');
      const branchScope = scopeAll ? 'all' : selectedBranches;
      const data: MembershipPlan = {
        id: plan ? plan.id : uid('mp'),
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
        active: plan ? plan.active : true,
      };

      if (plan) {
        const idx = d.membershipPlans.findIndex((x) => x.id === plan.id);
        if (idx >= 0) d.membershipPlans[idx] = data;
      } else {
        d.membershipPlans.push(data);
      }
    }, plan ? 'Đã cập nhật gói hội viên' : 'Đã thêm gói hội viên mới');
    onClose();
  };

  return (
    <Modal
      wide
      title={plan ? `Chỉnh sửa: ${plan.name}` : 'Thêm gói hội viên mới'}
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose}>Hủy</button>
          <button className="btn primary" onClick={handleSave} id="save-ms-plan">Lưu gói tập</button>
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
          <label>Thời hạn (tháng) *</label>
          <input className="input" type="number" min={1} value={durationMonths} onChange={(e) => setDurationMonths(+e.target.value)} />
        </div>
        <div className="field">
          <label>Cấp độ ưu tiên Waitlist (1: Thấp, 3: VIP) *</label>
          <select className="input" value={priority} onChange={(e) => setPriority(+e.target.value)}>
            <option value={1}>Cấp 1 (Gói Basic)</option>
            <option value={2}>Cấp 2 (Gói Tiêu chuẩn)</option>
            <option value={3}>Cấp 3 (Gói VIP - Ưu tiên hàng đầu)</option>
          </select>
        </div>
        <div className="field">
          <label>Số buổi PT tặng kèm (miễn phí)</label>
          <input className="input" type="number" min={0} value={freePTSessions} onChange={(e) => setFreePTSessions(+e.target.value)} />
        </div>
        <div className="field">
          <label>Giới hạn lớp học / tuần (0 = Không giới hạn)</label>
          <input className="input" type="number" min={0} value={classBookingsPerWeek} onChange={(e) => setClassBookingsPerWeek(+e.target.value)} />
        </div>
        <div className="field">
          <label>Số lần Freeze tối đa</label>
          <input className="input" type="number" min={0} value={maxFreezeTimes} onChange={(e) => setMaxFreezeTimes(+e.target.value)} />
        </div>
        <div className="field">
          <label>Tổng số ngày Freeze tối đa</label>
          <input className="input" type="number" min={0} value={maxFreezeDays} onChange={(e) => setMaxFreezeDays(+e.target.value)} />
        </div>
        <div className="field full">
          <label>Màu đại diện gói</label>
          <input type="color" value={color} onChange={(e) => setColor(e.target.value)} style={{ width: 80, height: 38, border: 'none', background: 'transparent', cursor: 'pointer' }} />
        </div>
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
              {db.branches.map((b) => {
                const sel = selectedBranches.includes(b.id);
                return (
                  <button
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
        <div className="field full">
          <label>Mô tả gói tập</label>
          <textarea className="input" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Tóm tắt đặc quyền của gói..." />
        </div>
      </div>
    </Modal>
  );
}

function PTPlanModal({ plan, onClose }: { plan: PTPlan | null; onClose: () => void }) {
  const { db, act } = useApp();
  const [name, setName] = useState(plan?.name || '');
  const [sessions, setSessions] = useState(plan?.sessions || 10);
  const [validityDays, setValidityDays] = useState(plan?.validityDays || 60);
  const [price, setPrice] = useState(plan?.price || 3000000);
  const [description, setDescription] = useState(plan?.description || '');
  const [scopeAll, setScopeAll] = useState(plan ? plan.branchScope === 'all' : true);
  const [selectedBranches, setSelectedBranches] = useState<string[]>(
    plan && Array.isArray(plan.branchScope) ? plan.branchScope : [db.branches[0]?.id || '']
  );

  const handleSave = () => {
    act((d) => {
      if (!name.trim()) throw new Error('Vui lòng nhập tên gói PT');
      const branchScope = scopeAll ? 'all' : selectedBranches;
      const data: PTPlan = {
        id: plan ? plan.id : uid('ptp'),
        name: name.trim(),
        sessions,
        validityDays,
        price,
        branchScope,
        description,
        active: plan ? plan.active : true,
      };

      if (plan) {
        const idx = d.ptPlans.findIndex((x) => x.id === plan.id);
        if (idx >= 0) d.ptPlans[idx] = data;
      } else {
        d.ptPlans.push(data);
      }
    }, plan ? 'Đã cập nhật gói PT' : 'Đã thêm gói PT mới');
    onClose();
  };

  return (
    <Modal
      title={plan ? `Chỉnh sửa: ${plan.name}` : 'Thêm gói PT mới'}
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose}>Hủy</button>
          <button className="btn primary" onClick={handleSave} id="save-pt-plan">Lưu gói PT</button>
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
              {db.branches.map((b) => {
                const sel = selectedBranches.includes(b.id);
                return (
                  <button
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
        <div className="field full">
          <label>Mô tả gói PT</label>
          <textarea className="input" value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
      </div>
    </Modal>
  );
}

/* ========================================================================== */
/*  4. Quản lý Lớp học Nhóm (AdminClasses)                                    */
/* ========================================================================== */
export { AdminClassesScreen as AdminClasses } from './screens/AdminClassesScreen';

/* ========================================================================== */
/*  5. Cấu hình Hệ thống (AdminSettings)                                      */
/* ========================================================================== */
export function AdminSettings() {
  const { db, act } = useApp();
  const [f, setF] = useState<Settings>({ ...db.settings });

  const handleSave = () => {
    act((d) => {
      d.settings = { ...f };
    }, 'Đã lưu cấu hình hệ thống');
  };

  return (
    <>
      <PageHead
        title="Cấu hình Hệ thống"
        desc="Thiết lập các mốc thời gian nhắc lịch, giới hạn hủy và xử lý danh sách chờ tự động."
        actions={
          <button className="btn primary" onClick={handleSave} id="btn-save-settings">
            <Save size={16} /> Lưu cấu hình
          </button>
        }
      />

      <div className="card" style={{ maxWidth: 760 }}>
        <div className="col" style={{ gap: 20 }}>
          <div className="field">
            <label>Thời gian nhắc lịch tập sắp diễn ra (giờ trước buổi tập)</label>
            <input
              type="number"
              min={1}
              max={24}
              className="input"
              value={f.reminderHours}
              onChange={(e) => setF({ ...f, reminderHours: +e.target.value })}
            />
            <span className="small dim">Mặc định 2 giờ. Gửi thông báo đến hội viên và HLV.</span>
          </div>

          <div className="field">
            <label>Thời hạn được hủy lịch hoàn trả lượt/buổi (giờ trước buổi tập)</label>
            <input
              type="number"
              min={1}
              max={48}
              className="input"
              value={f.cancelDeadlineHours}
              onChange={(e) => setF({ ...f, cancelDeadlineHours: +e.target.value })}
            />
            <span className="small dim">Mặc định 4 giờ. Hủy sau mốc này sẽ bị tính lượt (late cancelled).</span>
          </div>

          <div className="field">
            <label>Mốc hết hạn Danh sách chờ (Waitlist Cutoff) (giờ trước buổi tập)</label>
            <input
              type="number"
              min={1}
              max={24}
              className="input"
              value={f.waitlistCutoffHours}
              onChange={(e) => setF({ ...f, waitlistCutoffHours: +e.target.value })}
            />
            <span className="small dim">Theo nghiệp vụ: Khoảng 8 tiếng trước giờ học, nếu hội viên trong hàng chờ vẫn chưa được xếp chỗ thì thông báo lớp đã đầy và yêu cầu chọn lịch khác.</span>
          </div>

          <div className="field">
            <label>Cảnh báo gói hội viên / gói PT sắp hết hạn (ngày trước hạn)</label>
            <input
              type="number"
              min={1}
              max={30}
              className="input"
              value={f.expiryWarningDays}
              onChange={(e) => setF({ ...f, expiryWarningDays: +e.target.value })}
            />
            <span className="small dim">Gửi nhắc nhở gia hạn khi gói còn dưới số ngày này (mặc định 7 ngày).</span>
          </div>

          <div className="field">
            <label>Cảnh báo gói PT sắp hết số buổi</label>
            <input
              type="number"
              min={1}
              max={5}
              className="input"
              value={f.ptLowSessions}
              onChange={(e) => setF({ ...f, ptLowSessions: +e.target.value })}
            />
            <span className="small dim">Gửi thông báo khi gói PT còn dưới số buổi này (mặc định 2 buổi).</span>
          </div>
        </div>
      </div>
    </>
  );
}

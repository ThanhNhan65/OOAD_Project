import { useState, useEffect, useCallback } from 'react';
import {
  UserPlus, Lock, Unlock, Edit2, RefreshCw,
} from 'lucide-react';
import { useApp, mapApiUserToAppUser } from '../../../store/AppContext';
import { Avatar, Badge, Empty, Modal, PageHead, Tabs } from '../../../components/ui';
import { roleLabel } from '../../../components/labels';
import { fmtDate } from '../../../utils';
import type { User, Role } from '../../../types';
import { userService } from '../../../api/user.service';
import { branchesApi, type BranchApiDto } from '../../../api/branchesApi';

export function AdminUsersScreen() {
  const { user: currentUser, toast } = useApp();
  const [users, setUsers] = useState<User[]>([]);
  const [branches, setBranches] = useState<BranchApiDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState<Role | 'all'>('all');
  const [search, setSearch] = useState('');
  const [createModal, setCreateModal] = useState<boolean>(false);
  const [editingUser, setEditingUser] = useState<User | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [apiUsers, apiBranches] = await Promise.all([
        userService.getAllUsers(),
        branchesApi.getAll().catch(() => [] as BranchApiDto[]),
      ]);
      setBranches(apiBranches);
      setUsers(apiUsers.map((au) => mapApiUserToAppUser(au)));
    } catch (err) {
      toast((err as Error).message || 'Không thể tải danh sách người dùng từ máy chủ', 'error');
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const filteredUsers = users.filter((u) => {
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

  const toggleLock = async (targetUser: User) => {
    if (targetUser.id === currentUser?.id) {
      toast('Không thể tự khóa tài khoản của chính mình!', 'error');
      return;
    }
    try {
      const nextLocked = !targetUser.locked;
      await userService.updateUserStatus(targetUser.id, { locked: nextLocked });
      setUsers((prev) => prev.map((u) => (u.id === targetUser.id ? { ...u, locked: nextLocked } : u)));
      toast(nextLocked ? 'Đã khóa tài khoản' : 'Đã mở khóa tài khoản', 'success');
    } catch (err) {
      toast((err as Error).message || 'Không thể cập nhật trạng thái tài khoản', 'error');
    }
  };

  return (
    <>
      <PageHead
        title="Quản lý Tài khoản & Phân quyền"
        desc="Quản trị người dùng toàn hệ thống, tạo tài khoản nhân viên/HLV và phân quyền chi nhánh."
        actions={
          <div className="row" style={{ gap: 8 }}>
            <button className="btn ghost sm" onClick={loadData} disabled={loading}>
              <RefreshCw size={14} className={loading ? 'spin' : ''} /> Làm mới
            </button>
            <button className="btn primary" onClick={() => setCreateModal(true)} id="btn-add-staff">
              <UserPlus size={16} /> Thêm nhân viên / HLV
            </button>
          </div>
        }
      />

      <div className="row wrap mb16" style={{ justifyContent: 'space-between', gap: 12 }}>
        <Tabs
          value={tab}
          onChange={setTab}
          tabs={[
            { id: 'all', label: 'Tất cả', count: users.length },
            { id: 'member', label: 'Hội viên', count: users.filter((u) => u.role === 'member').length },
            { id: 'trainer', label: 'Huấn luyện viên', count: users.filter((u) => u.role === 'trainer').length },
            { id: 'receptionist', label: 'Lễ tân', count: users.filter((u) => u.role === 'receptionist').length },
            { id: 'admin', label: 'Quản trị viên', count: users.filter((u) => u.role === 'admin').length },
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
              const branch = u.branchId ? branches.find((b) => b.id === u.branchId) : null;
              const branchLabel = u.branchName || branch?.name;
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
                      branchLabel || 'Chưa gán'
                    ) : u.role === 'trainer' ? (
                      branchLabel ? branchLabel.replace('DFGymCenter ', '') : 'Chưa gán'
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
        {!loading && filteredUsers.length === 0 && <Empty text="Không tìm thấy người dùng phù hợp" />}
      </div>

      {createModal && (
        <CreateStaffModal
          branches={branches}
          onCreated={loadData}
          onClose={() => setCreateModal(false)}
        />
      )}
      {editingUser && (
        <EditUserModal
          user={editingUser}
          branches={branches}
          onUpdated={loadData}
          onClose={() => setEditingUser(null)}
        />
      )}
    </>
  );
}

function CreateStaffModal({
  branches,
  onCreated,
  onClose,
}: {
  branches: BranchApiDto[];
  onCreated: () => Promise<void>;
  onClose: () => void;
}) {
  const { toast } = useApp();
  const [role, setRole] = useState<'receptionist' | 'trainer' | 'admin'>('receptionist');
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [branchId, setBranchId] = useState(branches[0]?.id || '');
  const [specialties, setSpecialties] = useState('Gym, HIIT, Cardio');
  const [experienceYears, setExperienceYears] = useState(3);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!branchId && branches.length > 0) setBranchId(branches[0].id);
  }, [branches, branchId]);

  const handleSave = async () => {
    if (!fullName.trim() || !email.trim()) {
      toast('Vui lòng điền đủ họ tên và email', 'error');
      return;
    }
    setSaving(true);
    try {
      await userService.createStaff({
        fullName: fullName.trim(),
        email: email.trim(),
        phone: phone.trim(),
        role: role === 'receptionist' ? 'STAFF' : role === 'trainer' ? 'TRAINER' : 'ADMIN',
        branchId: role !== 'admin' ? branchId : undefined,
        specialty: role === 'trainer' ? specialties : undefined,
        experienceYears: role === 'trainer' ? experienceYears : undefined,
      });
      toast('Tạo tài khoản thành công! Mật khẩu mặc định là 123456', 'success');
      await onCreated();
      onClose();
    } catch (err) {
      toast((err as Error).message || 'Không thể tạo tài khoản', 'error');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      title="Tạo tài khoản nhân viên / HLV"
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose}>Hủy</button>
          <button className="btn primary" onClick={handleSave} disabled={saving} id="save-staff">
            {saving ? 'Đang tạo...' : 'Tạo tài khoản'}
          </button>
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
              {branches.map((b) => (
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

function EditUserModal({
  user,
  branches,
  onUpdated,
  onClose,
}: {
  user: User;
  branches: BranchApiDto[];
  onUpdated: () => Promise<void>;
  onClose: () => void;
}) {
  const { toast } = useApp();
  const [fullName, setFullName] = useState(user.fullName);
  const [phone, setPhone] = useState(user.phone);
  const [branchId, setBranchId] = useState(user.branchId || branches[0]?.id || '');
  const [saving, setSaving] = useState(false);

  const handleUpdate = async () => {
    setSaving(true);
    try {
      await userService.updateUserByAdmin(user.id, {
        fullName: fullName.trim(),
        phone: phone.trim(),
        branchId: user.role === 'receptionist' || user.role === 'trainer' ? branchId : undefined,
      });
      toast('Cập nhật thông tin người dùng thành công', 'success');
      await onUpdated();
      onClose();
    } catch (err) {
      toast((err as Error).message || 'Cập nhật thất bại', 'error');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      title={`Chỉnh sửa: ${user.fullName}`}
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose}>Hủy</button>
          <button className="btn primary" onClick={handleUpdate} disabled={saving} id="save-edit-user">
            {saving ? 'Đang lưu...' : 'Lưu thay đổi'}
          </button>
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
        {(user.role === 'receptionist' || user.role === 'trainer') && (
          <div className="field">
            <label>Chi nhánh làm việc</label>
            <select className="input" value={branchId} onChange={(e) => setBranchId(e.target.value)}>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
          </div>
        )}
      </div>
    </Modal>
  );
}

export default AdminUsersScreen;

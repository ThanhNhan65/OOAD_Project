import { useState } from 'react';
import { Save, KeyRound } from 'lucide-react';
import { useApp } from '../store/AppContext';
import { Avatar, PageHead, Badge } from '../components/ui';
import { roleLabel } from '../components/labels';
import { MemberCardWidget } from '../components/member-card';
import { userService, authService } from '../api';

export function AccountPage() {
  const { user, updateCurrentUser, toast } = useApp();
  const [fullName, setFullName] = useState(user?.fullName || '');
  const [phone, setPhone] = useState(user?.phone || '');
  const [pw, setPw] = useState({ old: '', n1: '', n2: '' });
  const [saving, setSaving] = useState(false);

  const saveProfile = async () => {
    if (!fullName.trim()) {
      toast('Họ tên không được để trống', 'error');
      return;
    }
    setSaving(true);
    try {
      await userService.updateMe({
        fullName: fullName.trim(),
        phone: phone.trim(),
      });
      updateCurrentUser({ fullName: fullName.trim(), phone: phone.trim() });
      toast('Đã lưu thông tin tài khoản', 'success');
    } catch (e) {
      toast((e as Error).message, 'error');
    } finally {
      setSaving(false);
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
    } catch (e) {
      toast((e as Error).message, 'error');
    }
  };

  if (!user) return null;

  const roleTitles: Record<string, { title: string; desc: string; duty?: string }> = {
    admin: {
      title: 'Hồ sơ Quản trị viên (Admin Profile)',
      desc: 'Thông tin tài khoản quản trị hệ thống DFGymCenter và bảo mật đăng nhập.',
      duty: 'Toàn quyền cấu hình hệ thống, quản lý tài khoản người dùng, chi nhánh và các gói dịch vụ.',
    },
    receptionist: {
      title: 'Hồ sơ Nhân viên Lễ tân (Staff Profile)',
      desc: 'Thông tin nhân sự ca trực tại quầy lễ tân và bảo mật tài khoản.',
      duty: 'Phụ trách tiếp đón hội viên, điểm danh check-in, cấp/khóa thẻ từ và tiếp nhận yêu cầu bảo lưu gói.',
    },
    trainer: {
      title: 'Hồ sơ Huấn luyện viên (Trainer Profile)',
      desc: 'Thông tin tài khoản HLV cá nhân và bảo mật đăng nhập.',
      duty: 'Phụ trách giảng dạy lớp nhóm, huấn luyện PT 1-kèm-1 và cập nhật lịch rảnh.',
    },
    member: {
      title: 'Hồ sơ Hội viên (Member Profile)',
      desc: 'Thông tin tài khoản hội viên, thẻ thành viên và bảo mật cá nhân.',
    },
  };

  const meta = roleTitles[user.role] || {
    title: 'Hồ sơ cá nhân',
    desc: 'Quản lý thông tin tài khoản và đổi mật khẩu bảo mật.',
  };

  return (
    <>
      <PageHead title={meta.title} desc={meta.desc} />

      <div className="grid g3">
        <div className="card" style={{ textAlign: 'center' }}>
          <div style={{ display: 'flex', justifyContent: 'center' }}>
            <Avatar user={user} size="lg" />
          </div>
          <h2 className="mt16">{user.fullName}</h2>
          <div className="small muted">{user.email}</div>
          <div className="mt16">
            <Badge color={roleLabel[user.role][1]}>{roleLabel[user.role][0]}</Badge>
          </div>
          {user.branchName && (
            <div className="small muted mt12">
              Chi nhánh làm việc: <b>{user.branchName}</b>
            </div>
          )}
          {meta.duty && (
            <div className="small dim mt12" style={{ padding: '8px 12px', background: 'var(--bg-subtle, rgba(255,255,255,0.04))', borderRadius: 8, textAlign: 'left', lineHeight: 1.4 }}>
              <b>Nhiệm vụ & Quyền hạn:</b><br />{meta.duty}
            </div>
          )}
        </div>

        <div className="card span2">
          <div className="card-head">
            <h2>Thông tin chi tiết</h2>
          </div>
          <div className="form-grid">
            <div className="field">
              <label>Họ và tên</label>
              <input className="input" value={fullName} onChange={(e) => setFullName(e.target.value)} />
            </div>
            <div className="field">
              <label>Email đăng nhập</label>
              <input className="input" value={user.email} disabled />
            </div>
            <div className="field">
              <label>Số điện thoại</label>
              <input className="input" value={phone} onChange={(e) => setPhone(e.target.value)} />
            </div>
            <div className="field">
              <label>Vai trò hệ thống</label>
              <input className="input" value={roleLabel[user.role][0]} disabled />
            </div>
            {user.branchName && (
              <div className="field full">
                <label>Chi nhánh trực thuộc</label>
                <input className="input" value={user.branchName} disabled />
              </div>
            )}
          </div>
          <div className="row mt16" style={{ justifyContent: 'flex-end' }}>
            <button className="btn primary" onClick={saveProfile} disabled={saving}>
              <Save size={16} /> {saving ? 'Đang lưu...' : 'Lưu thay đổi'}
            </button>
          </div>

          <div className="divider" />
          <h3 className="mb16"><KeyRound size={16} style={{ display: 'inline', verticalAlign: 'middle' }} /> Đổi mật khẩu</h3>
          <div className="grid g3" style={{ gap: 12 }}>
            <input
              type="password"
              className="input"
              placeholder="Mật khẩu hiện tại"
              value={pw.old}
              onChange={(e) => setPw({ ...pw, old: e.target.value })}
            />
            <input
              type="password"
              className="input"
              placeholder="Mật khẩu mới"
              value={pw.n1}
              onChange={(e) => setPw({ ...pw, n1: e.target.value })}
            />
            <input
              type="password"
              className="input"
              placeholder="Xác nhận"
              value={pw.n2}
              onChange={(e) => setPw({ ...pw, n2: e.target.value })}
            />
          </div>
          <div className="row mt16" style={{ justifyContent: 'flex-end' }}>
            <button className="btn" onClick={changePw}>
              Cập nhật mật khẩu
            </button>
          </div>
        </div>
      </div>

      {user.role === 'member' && (
        <div className="mt24">
          <MemberCardWidget memberId={user.id} showHistory />
        </div>
      )}
    </>
  );
}

export const ProfileForm = AccountPage;
export const PasswordChange = AccountPage;

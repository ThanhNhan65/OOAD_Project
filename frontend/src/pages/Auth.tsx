import { useState } from 'react';
import { Link, useNavigate, Navigate } from 'react-router-dom';
import { Mail, Lock, User as UserIcon, Phone, Zap, ArrowRight, ScanLine, Dumbbell, CalendarCheck, TrendingUp } from 'lucide-react';
import { useApp } from '../store/AppContext';
import * as L from '../store/logic';
import { homeOf } from '../components/Layout';
import { Avatar, Badge } from '../components/ui';
import { roleLabel } from '../components/labels';
import hero from '../assets/gym-hero.jpg';
import { authService } from '../api';
import type { Role } from '../types';

function Hero() {
  return (
    <div className="auth-hero" style={{ backgroundImage: `url(${hero})` }}>
      <div className="brand" style={{ padding: 0 }}>
        <div className="brand-logo"><Zap size={22} color="#fff" fill="#fff" /></div>
        <div className="brand-name" style={{ fontSize: 22 }}>OOAD<span className="grad-text">Center</span></div>
      </div>
      <div>
        <Badge color="pink">Chuỗi phòng gym đa chi nhánh</Badge>
        <h1 style={{ fontSize: 44, lineHeight: 1.1, marginTop: 16, maxWidth: 520 }}>
          Tập luyện thông minh,<br /><span className="grad-text">bứt phá mỗi ngày.</span>
        </h1>
        <p className="muted" style={{ maxWidth: 460, marginTop: 14, fontSize: 15 }}>
          Đặt lớp nhóm, hẹn PT 1-kèm-1, check-in bằng QR và theo dõi tiến độ cơ thể — tất cả trong một nền tảng.
        </p>
        <div className="row wrap mt24" style={{ gap: 26 }}>
          {[
            [<CalendarCheck size={18} />, 'Lớp nhóm & waitlist'],
            [<Dumbbell size={18} />, 'PT 1-kèm-1'],
            [<TrendingUp size={18} />, 'Biểu đồ tiến độ'],
          ].map(([i, t], k) => (
            <div key={k} className="row small" style={{ gap: 8 }}><span style={{ color: '#ff8fb0' }}>{i}</span>{t}</div>
          ))}
        </div>
      </div>
    </div>
  );
}

const DEMO = [
  ['hoivien@ooad.vn', 'm1'],
  ['pt@ooad.vn', 'tr1'],
  ['letan@ooad.vn', 'rc1'],
  ['admin@ooad.vn', 'admin'],
];

export function Login() {
  const { db, user, login, act, toast } = useApp();
  const nav = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  if (user) return <Navigate to={homeOf(user.role)} replace />;
  const submit = async (e?: React.FormEvent, em = email, pw = password) => {
    e?.preventDefault();
    try {
      // 1. Cố gắng đăng nhập qua API Backend
      try {
        const res = await authService.login({ email: em, password: pw });
        if (res && res.user) {
          act((d) => {
            let u = d.users.find((x) => x.email.toLowerCase() === res.user.email.toLowerCase() || x.id === res.user.id);
            if (!u) {
              u = {
                id: res.user.id,
                email: res.user.email,
                password: pw,
                fullName: res.user.fullName,
                phone: res.user.phone || '',
                role: res.user.role as Role,
                gender: (res.user.gender as 'male' | 'female' | 'other') || 'male',
                dob: res.user.dateOfBirth?.split('T')[0] || '2000-01-01',
                address: res.user.address || '',
                avatarColor: '#ec4899',
                createdAt: res.user.createdAt,
                branchId: res.user.branchId,
                locked: res.user.locked,
                cardCode: res.user.cardCode,
              };
              d.users.push(u);
            } else {
              u.locked = res.user.locked;
              u.fullName = res.user.fullName;
              u.phone = res.user.phone || u.phone;
              u.branchId = res.user.branchId || u.branchId;
              if (res.user.cardCode) u.cardCode = res.user.cardCode;
            }
          });
        }
      } catch (apiErr) {
        const msg = (apiErr as Error).message || '';
        if (msg.includes('LOCKED') || msg.includes('khóa') || msg.includes('không chính xác')) {
          toast(msg, 'error');
          return;
        }
      }

      const u = L.login(db, em, pw);
      if (u) {
        login(u);
        toast(`Xin chào, ${u.fullName}!`);
        nav(homeOf(u.role));
      }
    } catch (err) {
      toast((err as Error).message, 'error');
    }
  };
  return (
    <div className="auth-wrap">
      <Hero />
      <div className="auth-form">
        <div className="auth-box">
          <h1>Đăng nhập</h1>
          <p className="muted mt8">Chào mừng trở lại! Đăng nhập để tiếp tục hành trình tập luyện.</p>
          <form className="col mt24" style={{ gap: 14 }} onSubmit={submit}>
            <div className="field">
              <label>Email</label>
              <div className="input-icon"><Mail size={17} /><input id="login-email" className="input" type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="ban@email.com" required /></div>
            </div>
            <div className="field">
              <label>Mật khẩu</label>
              <div className="input-icon"><Lock size={17} /><input id="login-password" className="input" type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="••••••" required /></div>
            </div>
            <button className="btn primary lg block mt8" id="login-submit">Đăng nhập <ArrowRight size={18} /></button>
          </form>
          <p className="muted mt16" style={{ textAlign: 'center' }}>
            Chưa có tài khoản? <Link to="/register" className="grad-text bold" id="go-register">Đăng ký hội viên</Link>
          </p>
          <div className="divider" />
          <div className="small muted bold mb16">Tài khoản demo (mật khẩu: 123456)</div>
          <div className="col" style={{ gap: 8 }}>
            {DEMO.map(([em, id]) => {
              const u = db.users.find((x) => x.id === id);
              if (!u) return null;
              return (
                <button key={id} className="demo-acc" id={`demo-${u.role}`} onClick={() => submit(undefined, em, '123456')}>
                  <Avatar user={u} size="sm" />
                  <div className="grow">
                    <div className="bold" style={{ fontSize: 13 }}>{u.fullName}</div>
                    <div className="small dim">{em}</div>
                  </div>
                  <Badge color={roleLabel[u.role][1]}>{roleLabel[u.role][0]}</Badge>
                </button>
              );
            })}
          </div>
          <Link to="/kiosk" className="btn block mt16" id="go-kiosk"><ScanLine size={17} /> Mở máy quét check-in (Kiosk)</Link>
        </div>
      </div>
    </div>
  );
}

export function Register() {
  const { db, user, act, login, toast } = useApp();
  const nav = useNavigate();
  const [f, setF] = useState({
    role: 'member' as 'member' | 'trainer',
    branchId: db.branches[0]?.id || '',
    specialty: 'Gym, HIIT, Cardio',
    experienceYears: 2,
    fullName: '',
    email: '',
    phone: '',
    password: '',
    confirm: '',
    gender: 'male' as 'male' | 'female' | 'other',
    dob: '2000-01-01',
  });
  if (user) return <Navigate to={homeOf(user.role)} replace />;
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setF({ ...f, [k]: k === 'experienceYears' ? +e.target.value : e.target.value });
  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (f.password !== f.confirm) return toast('Mật khẩu xác nhận không khớp', 'error');
    if (f.role === 'trainer' && !f.branchId) return toast('Huấn luyện viên bắt buộc phải chọn Chi nhánh!', 'error');

    try {
      try {
        await authService.register({
          email: f.email,
          fullName: f.fullName,
          password: f.password,
          phone: f.phone,
          role: f.role === 'trainer' ? 'TRAINER' : 'MEMBER',
          branchId: f.role === 'trainer' ? f.branchId : undefined,
          dateOfBirth: f.dob,
          gender: f.gender,
          specialty: f.role === 'trainer' ? f.specialty : undefined,
          experienceYears: f.role === 'trainer' ? f.experienceYears : undefined,
        });
      } catch (apiErr) {
        const msg = (apiErr as Error).message || '';
        if (msg.includes('đã được sử dụng') || msg.includes('bắt buộc')) {
          toast(msg, 'error');
          return;
        }
      }

      const u = act((d) => L.register(d, f), 'Đăng ký thành công!');
      if (u) {
        login(u);
        nav(u.role === 'trainer' ? '/trainer/profile' : '/member/profile');
      }
    } catch (err) {
      toast((err as Error).message, 'error');
    }
  };
  return (
    <div className="auth-wrap">
      <Hero />
      <div className="auth-form">
        <form className="auth-box" onSubmit={submit}>
          <h1>Tạo tài khoản mới</h1>
          <p className="muted mt8">Đăng ký tài khoản Hội viên hoặc Huấn luyện viên (PT).</p>
          <div className="form-grid mt24">
            <div className="field full">
              <label>Vai trò đăng ký</label>
              <div className="chips">
                <button
                  type="button"
                  className={`chip ${f.role === 'member' ? 'on' : ''}`}
                  onClick={() => setF({ ...f, role: 'member' })}
                  id="reg-role-member"
                >
                  Hội viên (MEMBER)
                </button>
                <button
                  type="button"
                  className={`chip ${f.role === 'trainer' ? 'on' : ''}`}
                  onClick={() => setF({ ...f, role: 'trainer', branchId: f.branchId || db.branches[0]?.id || '' })}
                  id="reg-role-trainer"
                >
                  Huấn luyện viên (TRAINER)
                </button>
              </div>
            </div>
            {f.role === 'trainer' && (
              <>
                <div className="field full">
                  <label>Chi nhánh làm việc (Bắt buộc) *</label>
                  <select className="input" value={f.branchId} onChange={set('branchId')} required id="reg-branch">
                    {db.branches.map((b) => (
                      <option key={b.id} value={b.id}>{b.name}</option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label>Chuyên môn</label>
                  <input className="input" value={f.specialty} onChange={set('specialty')} placeholder="Gym, Yoga, HIIT..." />
                </div>
                <div className="field">
                  <label>Số năm kinh nghiệm</label>
                  <input type="number" min={0} className="input" value={f.experienceYears} onChange={set('experienceYears')} />
                </div>
              </>
            )}
            <div className="field full"><label>Họ và tên</label><div className="input-icon"><UserIcon size={17} /><input id="reg-name" className="input" value={f.fullName} onChange={set('fullName')} required placeholder="Nguyễn Văn A" /></div></div>
            <div className="field"><label>Email</label><div className="input-icon"><Mail size={17} /><input id="reg-email" type="email" className="input" value={f.email} onChange={set('email')} required /></div></div>
            <div className="field"><label>Số điện thoại</label><div className="input-icon"><Phone size={17} /><input id="reg-phone" className="input" value={f.phone} onChange={set('phone')} required pattern="0[0-9]{9}" title="10 chữ số, bắt đầu bằng 0" /></div></div>
            <div className="field"><label>Giới tính</label><select className="input" value={f.gender} onChange={set('gender')}><option value="male">Nam</option><option value="female">Nữ</option><option value="other">Khác</option></select></div>
            <div className="field"><label>Ngày sinh</label><input type="date" className="input" value={f.dob} onChange={set('dob')} /></div>
            <div className="field"><label>Mật khẩu</label><input id="reg-password" type="password" className="input" value={f.password} onChange={set('password')} required minLength={6} /></div>
            <div className="field"><label>Xác nhận mật khẩu</label><input id="reg-confirm" type="password" className="input" value={f.confirm} onChange={set('confirm')} required /></div>
          </div>
          <button className="btn primary lg block mt24" id="reg-submit">Đăng ký <ArrowRight size={18} /></button>
          <p className="muted mt16" style={{ textAlign: 'center' }}>Đã có tài khoản? <Link to="/login" className="grad-text bold">Đăng nhập</Link></p>
        </form>
      </div>
    </div>
  );
}

export const LoginForm = Login;
export const RegisterForm = Register;

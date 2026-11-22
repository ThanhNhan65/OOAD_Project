import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import type { DB, User } from '../types';
import { getAuthToken, setAuthToken } from '../api/apiClient';
import { userService } from '../api/user.service';
import type { ApiUserDto } from '../api/auth.service';

const LEGACY_DB_KEY = 'ooad_db_v4_hcm';
const LEGACY_SESSION_KEY = 'ooad_session_v1';
const API_USER_KEY = 'ooad_api_user';
const THEME_KEY = 'ooad_theme';

const EMPTY_DB: DB = {
  version: 5,
  users: [],
  branches: [],
  equipment: [],
  equipmentReports: [],
  membershipPlans: [],
  ptPlans: [],
  memberships: [],
  ptPackages: [],
  freezeRequests: [],
  payments: [],
  classes: [],
  sessions: [],
  classBookings: [],
  trainerSlots: [],
  ptBookings: [],
  checkins: [],
  bodyMetrics: [],
  reviews: [],
  notifications: [],
  memberCards: [],
  settings: {
    reminderHours: 2,
    cancelDeadlineHours: 4,
    waitlistCutoffHours: 8,
    expiryWarningDays: 7,
    ptLowSessions: 2,
  },
};

export function mapApiUserToAppUser(apiUser: ApiUserDto, existing?: Partial<User>): User {
  return {
    id: apiUser.id,
    role: apiUser.role,
    email: apiUser.email,
    password: '',
    fullName: apiUser.fullName,
    phone: apiUser.phone ?? existing?.phone ?? '',
    gender: (apiUser.gender as User['gender']) || existing?.gender || 'male',
    dob: apiUser.dateOfBirth ? apiUser.dateOfBirth.slice(0, 10) : (existing?.dob ?? '1998-01-01'),
    address: apiUser.address ?? existing?.address ?? '',
    avatarColor: existing?.avatarColor ?? '#7c5cff',
    locked: apiUser.locked || apiUser.status === 'LOCKED',
    createdAt: apiUser.createdAt || existing?.createdAt || new Date().toISOString(),
    memberId: apiUser.memberId ?? existing?.memberId,
    branchId: apiUser.branchId ?? existing?.branchId,
    branchName: apiUser.branchName ?? existing?.branchName,
    cardCode: apiUser.cardCode ?? existing?.cardCode,
    cardStatus: existing?.cardStatus ?? 'ACTIVE',
    trainer:
      apiUser.role === 'trainer'
        ? {
            specialties: (apiUser.specialty || 'Gym, Fitness')
              .split(',')
              .map((s) => s.trim())
              .filter(Boolean),
            experienceYears: apiUser.experienceYears ?? existing?.trainer?.experienceYears ?? 0,
            certifications: existing?.trainer?.certifications ?? ['HLV DFGymCenter'],
            bio: apiUser.bio ?? existing?.trainer?.bio ?? '',
            branchIds: apiUser.branchId ? [apiUser.branchId] : existing?.trainer?.branchIds ?? [],
          }
        : undefined,
  };
}

function loadStoredApiUser(): User | null {
  if (!getAuthToken()) return null;
  try {
    const raw = localStorage.getItem(API_USER_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<User> & Partial<ApiUserDto>;
    if (!parsed || !parsed.id || !parsed.role) return null;
    if ('status' in parsed && !('avatarColor' in parsed)) {
      return mapApiUserToAppUser(parsed as ApiUserDto);
    }
    return parsed as User;
  } catch {
    return null;
  }
}

export interface Toast {
  id: number;
  type: 'success' | 'error' | 'info' | 'warning';
  message: string;
}

interface Ctx {
  db: DB;
  user: User | null;
  act: <T>(fn: (draft: DB) => T, successMsg?: string | ((r: T) => string | undefined)) => T | undefined;
  login: (u: User) => void;
  logout: () => void;
  updateCurrentUser: (patch: Partial<User>) => void;
  toast: (message: string, type?: Toast['type']) => void;
  toasts: Toast[];
  theme: 'light' | 'dark';
  toggleTheme: () => void;
}

const AppCtx = createContext<Ctx>(null as unknown as Ctx);

export function AppProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => loadStoredApiUser());
  const [toasts, setToasts] = useState<Toast[]>([]);
  const [theme, setTheme] = useState<'light' | 'dark'>(() => {
    return (localStorage.getItem(THEME_KEY) as 'light' | 'dark') || 'light';
  });

  useEffect(() => {
    // Dọn triệt để dữ liệu seed/mock cũ khỏi localStorage
    localStorage.removeItem(LEGACY_DB_KEY);
    localStorage.removeItem(LEGACY_SESSION_KEY);
  }, []);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem(THEME_KEY, theme);
  }, [theme]);

  // Đồng bộ hồ sơ mới nhất từ API nếu đang có JWT
  useEffect(() => {
    if (!getAuthToken()) return;
    let cancelled = false;
    userService
      .getMe()
      .then((apiUser) => {
        if (cancelled) return;
        setUser((prev) => {
          const updated = mapApiUserToAppUser(apiUser, prev ?? undefined);
          localStorage.setItem(API_USER_KEY, JSON.stringify(updated));
          return updated;
        });
      })
      .catch(() => {
        if (!cancelled && !getAuthToken()) {
          localStorage.removeItem(API_USER_KEY);
          setUser(null);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const toggleTheme = useCallback(() => {
    setTheme((prev) => (prev === 'light' ? 'dark' : 'light'));
  }, []);

  const toast = useCallback((message: string, type: Toast['type'] = 'success') => {
    const id = Date.now() + Math.random();
    setToasts((t) => [...t, { id, type, message }]);
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 3800);
  }, []);

  const act = useCallback(
    <T,>(fn: (draft: DB) => T, successMsg?: string | ((r: T) => string | undefined)) => {
      const draft = structuredClone(EMPTY_DB);
      try {
        const res = fn(draft);
        const msg = typeof successMsg === 'function' ? successMsg(res) : successMsg;
        if (msg) toast(msg, 'success');
        return res;
      } catch (e) {
        toast((e as Error).message, 'error');
        return undefined;
      }
    },
    [toast],
  );

  const login = useCallback((u: User) => {
    localStorage.setItem(API_USER_KEY, JSON.stringify(u));
    setUser(u);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem(LEGACY_SESSION_KEY);
    localStorage.removeItem(LEGACY_DB_KEY);
    localStorage.removeItem(API_USER_KEY);
    setAuthToken(null);
    setUser(null);
  }, []);

  const updateCurrentUser = useCallback((patch: Partial<User>) => {
    setUser((prev) => {
      if (!prev) return prev;
      const next = { ...prev, ...patch };
      localStorage.setItem(API_USER_KEY, JSON.stringify(next));
      return next;
    });
  }, []);

  const value: Ctx = {
    db: EMPTY_DB,
    user,
    act,
    login,
    logout,
    updateCurrentUser,
    toast,
    toasts,
    theme,
    toggleTheme,
  };

  return <AppCtx.Provider value={value}>{children}</AppCtx.Provider>;
}

export const useApp = () => useContext(AppCtx);
export const useAuth = useApp;
export const AuthProvider = AppProvider;

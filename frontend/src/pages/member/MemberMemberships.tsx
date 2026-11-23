import { useCallback, useEffect, useMemo, useState } from 'react';
import { Check, Snowflake, RefreshCw, Dumbbell, Crown, CreditCard, History, Gift, MapPin } from 'lucide-react';
import { useApp } from '../../store/AppContext';
import * as L from '../../store/logic';
import { Badge, Modal, PageHead, PaymentModal, Tabs, Empty, Progress } from '../../components/ui';
import { mStatusLabel, reqLabel } from '../../components/labels';
import { fmtDate, fmtMoney, addDays, todayStr, diffDays, fmtDateTime } from '../../utils';
import {
  membershipPlansApi,
  type ApiMembershipPlanDto,
  type ApiMembershipDto,
  type ApiFreezeRequestDto,
} from '../../api/membershipPlansApi';
import { branchesApi, type BranchApiDto } from '../../api/branchesApi';
import { ptApi, type ApiPtBalanceDto } from '../../api/ptApi';
import { getAuthToken, isApiNetworkError } from '../../api/apiClient';
import type { MembershipPlan, PTPlan, PaymentMethod } from '../../types';

type LocalPay =
  | { kind: 'ms'; plan: MembershipPlan; branchId: string; retryId?: string }
  | { kind: 'pt'; plan: PTPlan; branchId: string; retryId?: string };

type ApiPay = {
  plan: ApiMembershipPlanDto;
  branchId: string;
  branchName: string;
  retryId?: string;
};

const apiStatusBadge = (status: string): [string, string] => {
  switch (status.toLowerCase()) {
    case 'active':
      return ['Đang hiệu lực', 'green'];
    case 'pending':
      return ['Chờ thanh toán', 'amber'];
    case 'frozen':
      return ['Đang tạm ngưng', 'purple'];
    case 'expired':
      return ['Hết hạn', 'gray'];
    case 'cancelled':
      return ['Đã hủy', 'red'];
    default:
      return [status, 'gray'];
  }
};

const apiFreezeBadge = (status: string): [string, string] => {
  switch (status.toLowerCase()) {
    case 'approved':
      return ['Đã duyệt', 'green'];
    case 'rejected':
      return ['Từ chối', 'red'];
    default:
      return ['Chờ duyệt', 'amber'];
  }
};

const priorityColor = (level: number) => (level >= 3 ? '#ff5c8a' : level === 2 ? '#7c5cff' : '#3fa9ff');

export function MemberMemberships() {
  const { db, user, act, toast } = useApp();
  const uid = user!.id;
  const [tab, setTab] = useState<'mine' | 'plans' | 'pt' | 'freeze'>('mine');

  // API state
  const [apiMode, setApiMode] = useState<boolean>(!!getAuthToken());
  const [loading, setLoading] = useState<boolean>(false);
  const [apiPlans, setApiPlans] = useState<ApiMembershipPlanDto[]>([]);
  const [apiBranches, setApiBranches] = useState<BranchApiDto[]>([]);
  const [apiMemberships, setApiMemberships] = useState<ApiMembershipDto[]>([]);
  const [apiFreezes, setApiFreezes] = useState<ApiFreezeRequestDto[]>([]);
  const [apiPtBalance, setApiPtBalance] = useState<ApiPtBalanceDto | null>(null);

  // Modals
  const [localPay, setLocalPay] = useState<LocalPay | null>(null);
  const [apiPay, setApiPay] = useState<ApiPay | null>(null);
  const [localBranchPick, setLocalBranchPick] = useState<{ plan: MembershipPlan | PTPlan; kind: 'ms' | 'pt' } | null>(null);
  const [apiBranchPick, setApiBranchPick] = useState<ApiMembershipPlanDto | null>(null);
  const [freezeFor, setFreezeFor] = useState<string | null>(null);

  const loadFromApi = useCallback(async () => {
    if (!getAuthToken()) {
      setApiMode(false);
      return;
    }
    setLoading(true);
    try {
      const [plans, branches, memberships, freezes, ptBal] = await Promise.all([
        membershipPlansApi.getAll(),
        branchesApi.getAll(),
        membershipPlansApi.getMyMemberships(),
        membershipPlansApi.getMyFreezeRequests(),
        ptApi.getPtBalance().catch(() => null),
      ]);
      setApiPlans(plans);
      setApiBranches(branches);
      setApiMemberships(memberships);
      setApiFreezes(freezes);
      setApiPtBalance(ptBal);
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
    loadFromApi();
  }, [loadFromApi]);

  // Derived API data
  const today = todayStr();
  const apiMsCatalog = useMemo(() => apiPlans.filter((p) => p.planType.toUpperCase() !== 'PT'), [apiPlans]);
  const apiPtCatalog = useMemo(() => apiPlans.filter((p) => p.planType.toUpperCase() === 'PT'), [apiPlans]);
  const apiGymList = useMemo(
    () =>
      apiMemberships
        .filter((m) => m.planType.toUpperCase() !== 'PT')
        .sort((a, b) => b.startDate.localeCompare(a.startDate)),
    [apiMemberships],
  );
  const apiPtList = useMemo(
    () =>
      apiMemberships
        .filter((m) => m.planType.toUpperCase() === 'PT')
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
    [apiMemberships],
  );
  const apiValidGymList = useMemo(
    () =>
      apiGymList.filter(
        (m) => ['active', 'frozen'].includes(m.status.toLowerCase()) && m.endDate.slice(0, 10) >= today,
      ),
    [apiGymList, today],
  );
  const apiLatestValidGym = useMemo(
    () => [...apiValidGymList].sort((a, b) => b.endDate.localeCompare(a.endDate))[0] ?? null,
    [apiValidGymList],
  );
  const apiHasValidGym = !!apiLatestValidGym;

  // Derived Local data
  const ms = db.memberships.filter((m) => m.memberId === uid).sort((a, b) => b.startDate.localeCompare(a.startDate));
  const pts = db.ptPackages.filter((p) => p.memberId === uid).sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  const freezes = db.freezeRequests.filter((f) => f.memberId === uid);
  const current = L.membershipOn(db, uid);
  const latest = L.latestPaidMembership(db, uid);
  const hasValidLocal = !!latest && latest.endDate >= today;
  const localBalance = L.ptBalance(db, uid);

  const hasValid = apiMode ? apiHasValidGym : hasValidLocal;
  const balance = apiMode && apiPtBalance
    ? { total: apiPtBalance.total, free: apiPtBalance.freeSessions, pkg: apiPtBalance.packageSessions }
    : localBalance;

  const pendingFreezeCount = apiMode
    ? apiFreezes.filter((f) => f.status.toLowerCase() === 'pending').length
    : freezes.filter((f) => f.status === 'pending').length;

  const doApiPay = async (method: PaymentMethod, success: boolean) => {
    if (!apiPay) return;
    try {
      const res = apiPay.retryId
        ? await membershipPlansApi.retryPayment(apiPay.retryId, {
            branchId: apiPay.branchId,
            paymentMethod: method,
            simulateSuccess: success,
          })
        : await membershipPlansApi.purchase({
            planId: apiPay.plan.id,
            branchId: apiPay.branchId,
            paymentMethod: method,
            simulateSuccess: success,
          });

      toast(res.message, res.success ? 'success' : 'error');
      setApiPay(null);
      await loadFromApi();
      if (res.success) setTab('mine');
    } catch (err) {
      toast((err as Error).message, 'error');
      setApiPay(null);
    }
  };

  const doLocalPay = (method: PaymentMethod, success: boolean) => {
    if (!localPay) return;
    act(
      (d) => {
        if (localPay.kind === 'ms') {
          return localPay.retryId
            ? L.payMembership(d, localPay.retryId, localPay.branchId, method, success)
            : L.purchaseMembership(d, uid, localPay.plan.id, localPay.branchId, method, success);
        }
        return localPay.retryId
          ? L.payPT(d, localPay.retryId, localPay.branchId, method, success)
          : L.purchasePT(d, uid, localPay.plan.id, localPay.branchId, method, success);
      },
      (ok) => (ok ? 'Thanh toán thành công – gói đã được kích hoạt!' : undefined),
    );
    if (!success) toast('Thanh toán thất bại – gói chưa được kích hoạt. Bạn có thể thanh toán lại.', 'error');
    setLocalPay(null);
    if (success) setTab('mine');
  };

  const chooseApiPlan = (plan: ApiMembershipPlanDto) => {
    if (plan.planType.toUpperCase() === 'PT' && !apiHasValidGym) {
      toast('Bạn cần có gói hội viên còn hiệu lực để mua gói PT', 'error');
      return;
    }
    const allowed = plan.isAllBranches
      ? apiBranches
      : apiBranches.filter((b) => plan.branchIds.includes(b.id));
    if (allowed.length <= 1) {
      const br = allowed[0] ?? apiBranches[0];
      setApiPay({
        plan,
        branchId: br?.id ?? '',
        branchName: br?.name ?? 'Hệ thống DFGymCenter',
      });
    } else {
      setApiBranchPick(plan);
    }
  };

  const chooseLocalPlan = (plan: MembershipPlan | PTPlan, kind: 'ms' | 'pt') => {
    if (kind === 'pt' && !current) {
      act(() => { throw new Error('Bạn cần có gói hội viên còn hiệu lực để mua gói PT'); });
      return;
    }
    const branches = plan.branchScope === 'all'
      ? db.branches
      : db.branches.filter((b) => (plan.branchScope as string[]).includes(b.id));
    if (branches.length === 1) {
      setLocalPay(
        kind === 'ms'
          ? { kind, plan: plan as MembershipPlan, branchId: branches[0].id }
          : { kind, plan: plan as PTPlan, branchId: branches[0].id },
      );
    } else {
      setLocalBranchPick({ plan, kind });
    }
  };

  const apiPlanScopeLabel = (p: ApiMembershipPlanDto) =>
    p.isAllBranches ? 'Tất cả chi nhánh' : p.branchNames.length > 0 ? p.branchNames.join(', ') : 'Theo chi nhánh đã chọn';

  const selectedApiMembershipForFreeze = apiMode && freezeFor
    ? apiMemberships.find((x) => x.id === freezeFor) ?? null
    : null;

  return (
    <>
      <PageHead
        title="Gói tập của tôi"
        desc="Mua, gia hạn, tạm ngưng gói hội viên và quản lý gói PT 1-kèm-1."
        actions={
          apiMode ? (
            <button className="btn sm" onClick={loadFromApi} disabled={loading}>
              <RefreshCw size={14} className={loading ? 'spin' : ''} /> Làm mới
            </button>
          ) : undefined
        }
      />

      <Tabs
        value={tab}
        onChange={setTab}
        tabs={[
          { id: 'mine', label: <><CreditCard size={15} /> Gói của tôi</> },
          { id: 'plans', label: <><Crown size={15} /> {hasValid ? 'Gia hạn / mua gói' : 'Mua gói hội viên'}</> },
          { id: 'pt', label: <><Dumbbell size={15} /> Gói PT 1-kèm-1</> },
          { id: 'freeze', label: <><Snowflake size={15} /> Yêu cầu freeze</>, count: pendingFreezeCount },
        ]}
      />

      {tab === 'mine' && (
        <div className="col" style={{ gap: 18 }}>
          <div className="grid g3">
            <div className="card">
              <div className="small muted">Buổi PT khả dụng</div>
              <div style={{ fontSize: 30, fontWeight: 800 }} className="grad-text">{balance.total}</div>
              <div className="row small muted mt8">
                <Gift size={14} /> {balance.free} tặng kèm (dùng trước) · <Dumbbell size={14} /> {balance.pkg} từ gói PT
              </div>
            </div>
            <div className="card">
              <div className="small muted">Hạn gói hội viên</div>
              <div style={{ fontSize: 30, fontWeight: 800 }}>
                {apiMode
                  ? apiLatestValidGym ? fmtDate(apiLatestValidGym.endDate.slice(0, 10)) : '—'
                  : latest ? fmtDate(latest.endDate) : '—'}
              </div>
              <div className="small muted mt8">
                {apiMode
                  ? apiHasValidGym
                    ? `Còn ${Math.max(0, diffDays(today, apiLatestValidGym!.endDate.slice(0, 10)))} ngày (tính cả gói nối tiếp)`
                    : 'Chưa có gói hiệu lực'
                  : hasValidLocal
                    ? `Còn ${diffDays(today, latest!.endDate)} ngày (tính cả gói nối tiếp)`
                    : 'Chưa có gói hiệu lực'}
              </div>
            </div>
            <div className="card row between">
              <div>
                <div className="small muted">Gia hạn nhanh</div>
                <div className="small muted mt8">Gói mới nối tiếp từ ngày hết hạn hiện tại.</div>
              </div>
              <button className="btn primary" onClick={() => setTab('plans')} id="quick-renew">
                <RefreshCw size={16} /> Gia hạn
              </button>
            </div>
          </div>

          <div className="card">
            <div className="card-head"><h2><History size={18} /> Gói hội viên & lịch sử</h2></div>
            {apiMode ? (
              apiGymList.length === 0 ? <Empty text="Bạn chưa mua gói nào" /> : (
                <div className="table-wrap">
                  <table>
                    <thead><tr><th>Gói</th><th>Thời hạn</th><th>Phạm vi</th><th>PT tặng</th><th>Freeze</th><th>Trạng thái</th><th></th></tr></thead>
                    <tbody>
                      {apiGymList.map((m) => {
                        const badge = apiStatusBadge(m.status);
                        const planObj = apiPlans.find((x) => x.id === m.planId);
                        const canRetry = m.status.toLowerCase() === 'pending';
                        const canFreeze = ['active', 'frozen'].includes(m.status.toLowerCase()) && m.maxFreezeTimes > 0;
                        return (
                          <tr key={m.id}>
                            <td>
                              <div className="row">
                                <span style={{ width: 10, height: 10, borderRadius: 4, background: priorityColor(m.priorityLevel) }} />
                                <b>{m.planName}</b>
                                {m.isRenewal && <Badge color="gray" plain>Gia hạn</Badge>}
                              </div>
                            </td>
                            <td>{m.payStatus === 'paid' ? `${fmtDate(m.startDate.slice(0, 10))} → ${fmtDate(m.endDate.slice(0, 10))}` : '—'}</td>
                            <td className="small">{m.isAllBranches ? 'Tất cả chi nhánh' : m.branchName || 'Theo chi nhánh'}</td>
                            <td>{m.totalPtSessions ? `${m.remainingSessions}/${m.totalPtSessions}` : '—'}</td>
                            <td className="small">
                              {m.maxFreezeTimes ? `${m.freezeCountUsed}/${m.maxFreezeTimes} lần · ${m.freezeDaysUsed}/${m.maxFreezeDays} ngày` : 'Không hỗ trợ'}
                            </td>
                            <td><Badge color={badge[1]}>{badge[0]}</Badge></td>
                            <td style={{ textAlign: 'right' }}>
                              {canRetry && planObj && (
                                <button
                                  className="btn sm primary"
                                  onClick={() => setApiPay({
                                    plan: planObj,
                                    branchId: m.branchId ?? apiBranches[0]?.id ?? '',
                                    branchName: m.branchName ?? apiBranches[0]?.name ?? 'Chi nhánh',
                                    retryId: m.id,
                                  })}
                                >
                                  Thanh toán lại
                                </button>
                              )}
                              {canFreeze && (
                                <button className="btn sm" onClick={() => setFreezeFor(m.id)} id={`freeze-${m.id}`}>
                                  <Snowflake size={14} /> Freeze
                                </button>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )
            ) : ms.length === 0 ? <Empty text="Bạn chưa mua gói nào" /> : (
              <div className="table-wrap">
                <table>
                  <thead><tr><th>Gói</th><th>Thời hạn</th><th>Phạm vi</th><th>PT tặng</th><th>Freeze</th><th>Trạng thái</th><th></th></tr></thead>
                  <tbody>
                    {ms.map((m) => {
                      const p = L.planById(db, m.planId)!;
                      const st = L.membershipStatus(db, m);
                      return (
                        <tr key={m.id}>
                          <td><div className="row"><span style={{ width: 10, height: 10, borderRadius: 4, background: p.color }} /><b>{p.name}</b>{m.isRenewal && <Badge color="gray" plain>Gia hạn</Badge>}</div></td>
                          <td>{m.payStatus === 'paid' ? `${fmtDate(m.startDate)} → ${fmtDate(m.endDate)}` : '—'}</td>
                          <td className="small">{L.scopeLabel(db, p.branchScope)}</td>
                          <td>{m.freeSessionsTotal ? `${m.freeSessionsTotal - m.freeSessionsUsed}/${m.freeSessionsTotal}` : '—'}</td>
                          <td className="small">{p.maxFreezeTimes ? `${m.freezeCountUsed}/${p.maxFreezeTimes} lần · ${m.freezeDaysUsed}/${p.maxFreezeDays} ngày` : 'Không hỗ trợ'}</td>
                          <td><Badge color={mStatusLabel[st][1]}>{mStatusLabel[st][0]}</Badge></td>
                          <td style={{ textAlign: 'right' }}>
                            {st === 'pending_payment' && (
                              <button className="btn sm primary" onClick={() => setLocalPay({ kind: 'ms', plan: p, branchId: p.branchScope === 'all' ? 'br1' : p.branchScope[0], retryId: m.id })}>
                                Thanh toán lại
                              </button>
                            )}
                            {['active', 'upcoming', 'frozen'].includes(st) && p.maxFreezeTimes > 0 && (
                              <button className="btn sm" onClick={() => setFreezeFor(m.id)} id={`freeze-${m.id}`}><Snowflake size={14} /> Freeze</button>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          <div className="card">
            <div className="card-head">
              <h2><Dumbbell size={18} /> Gói PT đã mua</h2>
              <button className="btn sm" onClick={() => setTab('pt')}>Mua thêm</button>
            </div>
            {apiMode ? (
              apiPtList.length === 0 ? <Empty text="Chưa có gói PT" /> : (
                <div className="grid g-auto">
                  {apiPtList.map((p) => {
                    const used = Math.max(0, p.totalPtSessions - p.remainingSessions);
                    const expired = p.payStatus === 'paid' && p.endDate.slice(0, 10) < today;
                    const usedUp = p.remainingSessions <= 0;
                    const label: [string, string] =
                      p.payStatus === 'pending_payment' ? ['Chờ thanh toán', 'amber']
                        : expired ? ['Hết thời hạn', 'gray']
                        : usedUp ? ['Đã dùng hết', 'gray']
                        : !apiHasValidGym ? ['Bảo lưu', 'purple']
                        : ['Đang hiệu lực', 'green'];
                    const planObj = apiPlans.find((x) => x.id === p.planId);
                    return (
                      <div className="card" key={p.id} style={{ background: 'rgba(0,209,178,.05)' }}>
                        <div className="row between"><b>{p.planName}</b><Badge color={label[1]}>{label[0]}</Badge></div>
                        {p.payStatus === 'paid' ? (
                          <>
                            <div className="row between mt16 small"><span className="muted">Đã dùng</span><b>{used}/{p.totalPtSessions} buổi</b></div>
                            <div className="mt8"><Progress value={p.totalPtSessions ? (used / p.totalPtSessions) * 100 : 0} color="linear-gradient(90deg,#00d1b2,#3fa9ff)" /></div>
                            <div className="small muted mt8">Hạn dùng: {fmtDate(p.startDate.slice(0, 10))} → {fmtDate(p.endDate.slice(0, 10))}</div>
                            {!expired && !usedUp && !apiHasValidGym && (
                              <div className="small mt8" style={{ color: '#b8a6ff' }}>Buổi còn lại được bảo lưu, dùng lại khi bạn có gói hội viên hiệu lực.</div>
                            )}
                          </>
                        ) : planObj ? (
                          <button
                            className="btn primary sm mt16"
                            onClick={() => setApiPay({
                              plan: planObj,
                              branchId: p.branchId ?? apiBranches[0]?.id ?? '',
                              branchName: p.branchName ?? apiBranches[0]?.name ?? 'Chi nhánh',
                              retryId: p.id,
                            })}
                          >
                            Thanh toán lại
                          </button>
                        ) : null}
                      </div>
                    );
                  })}
                </div>
              )
            ) : pts.length === 0 ? <Empty text="Chưa có gói PT" /> : (
              <div className="grid g-auto">
                {pts.map((p) => {
                  const plan = L.ptPlanById(db, p.ptPlanId)!;
                  const valid = L.ptPackageValid(p);
                  const expired = p.payStatus === 'paid' && p.endDate < today;
                  const usedUp = p.sessionsUsed >= p.sessionsTotal;
                  const label: [string, string] = p.payStatus === 'pending_payment' ? ['Chờ thanh toán', 'amber'] : expired ? ['Hết thời hạn', 'gray'] : usedUp ? ['Đã dùng hết', 'gray'] : !current ? ['Bảo lưu', 'purple'] : ['Đang hiệu lực', 'green'];
                  return (
                    <div className="card" key={p.id} style={{ background: 'rgba(0,209,178,.05)' }}>
                      <div className="row between"><b>{plan.name}</b><Badge color={label[1]}>{label[0]}</Badge></div>
                      {p.payStatus === 'paid' ? (
                        <>
                          <div className="row between mt16 small"><span className="muted">Đã dùng</span><b>{p.sessionsUsed}/{p.sessionsTotal} buổi</b></div>
                          <div className="mt8"><Progress value={(p.sessionsUsed / p.sessionsTotal) * 100} color="linear-gradient(90deg,#00d1b2,#3fa9ff)" /></div>
                          <div className="small muted mt8">Hạn dùng: {fmtDate(p.startDate)} → {fmtDate(p.endDate)}</div>
                          {valid && !current && <div className="small mt8" style={{ color: '#b8a6ff' }}>Buổi còn lại được bảo lưu, dùng lại khi bạn có gói hội viên hiệu lực.</div>}
                        </>
                      ) : (
                        <button className="btn primary sm mt16" onClick={() => setLocalPay({ kind: 'pt', plan, branchId: 'br1', retryId: p.id })}>Thanh toán lại</button>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>
      )}

      {tab === 'plans' && (
        <>
          {hasValid && (
            <div className="card mb16 small muted">
              🔁 Bạn đang có gói đến{' '}
              <b>
                {apiMode ? fmtDate(apiLatestValidGym!.endDate.slice(0, 10)) : fmtDate(latest!.endDate)}
              </b>
              . Gói mua thêm sẽ được <b>nối tiếp</b> từ ngày{' '}
              {fmtDate(addDays(apiMode ? apiLatestValidGym!.endDate.slice(0, 10) : latest!.endDate, 1))}.
            </div>
          )}
          <div className="grid g-auto">
            {apiMode
              ? apiMsCatalog.map((p) => {
                  const color = priorityColor(p.priorityLevel);
                  return (
                    <div className="card plan-card hover" key={p.id}>
                      <div className="plan-top" style={{ background: color }} />
                      {p.priorityLevel >= 3 && <div className="ribbon">HOT</div>}
                      <div className="row"><Crown size={18} color={color} /><h2>{p.name}</h2></div>
                      <div className="price">{fmtMoney(p.price)}</div>
                      <div className="small dim">/{p.durationDays} ngày · ≈ {fmtMoney(Math.round(p.price / Math.max(1, p.durationMonths)))}/tháng</div>
                      <ul>
                        <li><MapPin size={15} />{apiPlanScopeLabel(p)}</li>
                        <li><Check size={15} />{p.maxBookingsPerMonth ? `${p.maxBookingsPerMonth} lượt lớp nhóm/tháng` : 'Lớp nhóm không giới hạn'}</li>
                        <li><Check size={15} />{p.ptSessions ? `Tặng kèm ${p.ptSessions} buổi PT 1-kèm-1` : 'Tập gym & phòng máy tiêu chuẩn'}</li>
                        <li><Check size={15} />{p.maxFreezeTimes ? `Tạm ngưng tối đa ${p.maxFreezeTimes} lần (${p.maxFreezeDays} ngày)` : 'Không hỗ trợ tạm ngưng'}</li>
                      </ul>
                      <button className="btn primary block" onClick={() => chooseApiPlan(p)} id={`buy-${p.id}`}>
                        {hasValid ? 'Gia hạn bằng gói này' : 'Mua ngay'}
                      </button>
                    </div>
                  );
                })
              : db.membershipPlans.filter((p) => p.active).map((p) => (
                  <div className="card plan-card hover" key={p.id}>
                    <div className="plan-top" style={{ background: p.color }} />
                    {p.priority >= 3 && <div className="ribbon">HOT</div>}
                    <div className="row"><Crown size={18} color={p.color} /><h2>{p.name}</h2></div>
                    <div className="small muted mt8">{p.description}</div>
                    <div className="price">{fmtMoney(p.price)}</div>
                    <div className="small dim">/{p.durationMonths} tháng · ≈ {fmtMoney(Math.round(p.price / p.durationMonths))}/tháng</div>
                    <ul>
                      <li><MapPin size={15} />{L.scopeLabel(db, p.branchScope)}</li>
                      {p.benefits.map((b) => <li key={b}><Check size={15} />{b}</li>)}
                    </ul>
                    <button className="btn primary block" onClick={() => chooseLocalPlan(p, 'ms')} id={`buy-${p.id}`}>
                      {hasValid ? 'Gia hạn bằng gói này' : 'Mua ngay'}
                    </button>
                  </div>
                ))}
          </div>
        </>
      )}

      {tab === 'pt' && (
        <>
          <div className="card mb16 small muted">
            💡 Gói PT dùng theo số buổi trong thời hạn nhất định, không áp dụng freeze. Chỉ mua/sử dụng được khi có gói hội viên hiệu lực; khi gói hội viên hết hạn, buổi còn lại được bảo lưu đến hết hạn gói PT.
          </div>
          <div className="grid g-auto">
            {apiMode
              ? apiPtCatalog.map((p) => (
                  <div className="card plan-card hover" key={p.id}>
                    <div className="plan-top" style={{ background: 'var(--grad-teal)' }} />
                    <div className="row"><Dumbbell size={18} color="#00d1b2" /><h2>{p.name}</h2></div>
                    <div className="price">{fmtMoney(p.price)}</div>
                    <div className="small dim">{fmtMoney(Math.round(p.price / Math.max(1, p.ptSessions)))}/buổi</div>
                    <ul>
                      <li><Check size={15} />{p.ptSessions} buổi tập 1-kèm-1</li>
                      <li><Check size={15} />Sử dụng trong {p.durationDays} ngày</li>
                      <li><MapPin size={15} />{apiPlanScopeLabel(p)}</li>
                    </ul>
                    <button
                      className="btn primary block"
                      disabled={!apiHasValidGym}
                      onClick={() => chooseApiPlan(p)}
                      id={`buy-${p.id}`}
                    >
                      {apiHasValidGym ? 'Mua gói PT' : 'Cần gói hội viên hiệu lực'}
                    </button>
                  </div>
                ))
              : db.ptPlans.filter((p) => p.active).map((p) => (
                  <div className="card plan-card hover" key={p.id}>
                    <div className="plan-top" style={{ background: 'var(--grad-teal)' }} />
                    <div className="row"><Dumbbell size={18} color="#00d1b2" /><h2>{p.name}</h2></div>
                    <div className="small muted mt8">{p.description}</div>
                    <div className="price">{fmtMoney(p.price)}</div>
                    <div className="small dim">{fmtMoney(Math.round(p.price / p.sessions))}/buổi</div>
                    <ul>
                      <li><Check size={15} />{p.sessions} buổi tập 1-kèm-1</li>
                      <li><Check size={15} />Sử dụng trong {p.validityDays} ngày</li>
                      <li><MapPin size={15} />{L.scopeLabel(db, p.branchScope)}</li>
                    </ul>
                    <button className="btn primary block" disabled={!current} onClick={() => chooseLocalPlan(p, 'pt')} id={`buy-${p.id}`}>
                      {current ? 'Mua gói PT' : 'Cần gói hội viên hiệu lực'}
                    </button>
                  </div>
                ))}
          </div>
        </>
      )}

      {tab === 'freeze' && (
        <div className="card">
          <div className="card-head"><h2><Snowflake size={18} /> Lịch sử yêu cầu tạm ngưng</h2></div>
          {apiMode ? (
            apiFreezes.length === 0 ? <Empty text="Chưa có yêu cầu freeze nào" /> : (
              <div className="table-wrap">
                <table>
                  <thead><tr><th>Gửi lúc</th><th>Gói</th><th>Khoảng tạm ngưng</th><th>Số ngày</th><th>Lý do</th><th>Trạng thái</th><th>Người xử lý</th></tr></thead>
                  <tbody>
                    {apiFreezes.map((f) => {
                      const badge = apiFreezeBadge(f.status);
                      return (
                        <tr key={f.id}>
                          <td className="small">{fmtDateTime(f.createdAt)}</td>
                          <td>{f.planName}</td>
                          <td>{fmtDate(f.fromDate.slice(0, 10))} → {fmtDate(f.toDate.slice(0, 10))}</td>
                          <td>{f.days}</td>
                          <td className="small muted">{f.reason}</td>
                          <td><Badge color={badge[1]}>{badge[0]}</Badge></td>
                          <td className="small">{f.handledByName || '—'}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )
          ) : freezes.length === 0 ? <Empty text="Chưa có yêu cầu freeze nào" /> : (
            <div className="table-wrap">
              <table>
                <thead><tr><th>Gửi lúc</th><th>Gói</th><th>Khoảng tạm ngưng</th><th>Số ngày</th><th>Lý do</th><th>Trạng thái</th><th>Phản hồi</th></tr></thead>
                <tbody>
                  {freezes.map((f) => {
                    const m = db.memberships.find((x) => x.id === f.membershipId);
                    return (
                      <tr key={f.id}>
                        <td className="small">{fmtDateTime(f.createdAt)}</td>
                        <td>{L.planById(db, m?.planId ?? '')?.name}</td>
                        <td>{fmtDate(f.startDate)} → {fmtDate(f.endDate)}</td>
                        <td>{f.days}</td>
                        <td className="small muted">{f.reason}</td>
                        <td><Badge color={reqLabel[f.status][1]}>{reqLabel[f.status][0]}</Badge></td>
                        <td className="small">{f.note || '—'}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {apiBranchPick && (
        <Modal title="Chọn chi nhánh đăng ký" onClose={() => setApiBranchPick(null)}>
          <p className="muted mb16">
            Chi nhánh nơi bạn thanh toán (dùng cho thống kê). Quyền tập theo phạm vi gói: <b>{apiPlanScopeLabel(apiBranchPick)}</b>.
          </p>
          <div className="col">
            {(apiBranchPick.isAllBranches ? apiBranches : apiBranches.filter((b) => apiBranchPick.branchIds.includes(b.id))).map((b) => (
              <button
                key={b.id}
                className="demo-acc"
                onClick={() => {
                  setApiPay({ plan: apiBranchPick, branchId: b.id, branchName: b.name });
                  setApiBranchPick(null);
                }}
              >
                <MapPin size={18} color="#a78bfa" /><div><b>{b.name}</b><div className="small dim">{b.address}</div></div>
              </button>
            ))}
          </div>
        </Modal>
      )}

      {localBranchPick && (
        <Modal title="Chọn chi nhánh đăng ký" onClose={() => setLocalBranchPick(null)}>
          <p className="muted mb16">
            Chi nhánh nơi bạn thanh toán (dùng cho thống kê). Quyền tập theo phạm vi gói: <b>{L.scopeLabel(db, localBranchPick.plan.branchScope)}</b>.
          </p>
          <div className="col">
            {(localBranchPick.plan.branchScope === 'all' ? db.branches : db.branches.filter((b) => (localBranchPick.plan.branchScope as string[]).includes(b.id))).map((b) => (
              <button
                key={b.id}
                className="demo-acc"
                onClick={() => {
                  setLocalPay(
                    localBranchPick.kind === 'ms'
                      ? { kind: 'ms', plan: localBranchPick.plan as MembershipPlan, branchId: b.id }
                      : { kind: 'pt', plan: localBranchPick.plan as PTPlan, branchId: b.id },
                  );
                  setLocalBranchPick(null);
                }}
              >
                <MapPin size={18} color="#a78bfa" /><div><b>{b.name}</b><div className="small dim">{b.address}</div></div>
              </button>
            ))}
          </div>
        </Modal>
      )}

      {apiPay && (
        <PaymentModal
          title={
            apiPay.plan.planType.toUpperCase() === 'PT'
              ? `Gói PT ${apiPay.plan.name}`
              : `${hasValid && !apiPay.retryId ? 'Gia hạn' : 'Gói hội viên'} ${apiPay.plan.name}`
          }
          amount={apiPay.plan.price}
          lines={[
            ['Chi nhánh', apiPay.branchName],
            apiPay.plan.planType.toUpperCase() !== 'PT'
              ? ['Hiệu lực từ', fmtDate(apiHasValidGym && apiLatestValidGym ? addDays(apiLatestValidGym.endDate.slice(0, 10), 1) : today)]
              : ['Số buổi', `${apiPay.plan.ptSessions} buổi / ${apiPay.plan.durationDays} ngày`],
          ]}
          onClose={() => setApiPay(null)}
          onPay={doApiPay}
        />
      )}

      {localPay && (
        <PaymentModal
          title={localPay.kind === 'ms' ? `${hasValid && !localPay.retryId ? 'Gia hạn' : 'Gói hội viên'} ${localPay.plan.name}` : `Gói PT ${localPay.plan.name}`}
          amount={localPay.plan.price}
          lines={[
            ['Chi nhánh', L.branchById(db, localPay.branchId)?.name ?? ''],
            localPay.kind === 'ms'
              ? ['Hiệu lực từ', fmtDate(hasValidLocal && latest ? addDays(latest.endDate, 1) : today)]
              : ['Số buổi', `${(localPay.plan as PTPlan).sessions} buổi / ${(localPay.plan as PTPlan).validityDays} ngày`],
          ]}
          onClose={() => setLocalPay(null)}
          onPay={doLocalPay}
        />
      )}

      {freezeFor && (
        <FreezeModal
          membershipId={freezeFor}
          apiMembership={selectedApiMembershipForFreeze}
          onSaved={loadFromApi}
          onClose={() => setFreezeFor(null)}
        />
      )}
    </>
  );
}

function FreezeModal({
  membershipId,
  apiMembership,
  onSaved,
  onClose,
}: {
  membershipId: string;
  apiMembership: ApiMembershipDto | null;
  onSaved: () => Promise<void>;
  onClose: () => void;
}) {
  const { db, user, act, toast } = useApp();
  const localMs = apiMembership ? null : db.memberships.find((x) => x.id === membershipId)!;
  const localPlan = localMs ? L.planById(db, localMs.planId)! : null;

  const endDateStr = apiMembership ? apiMembership.endDate.slice(0, 10) : localMs!.endDate;
  const maxTimes = apiMembership ? apiMembership.maxFreezeTimes : localPlan!.maxFreezeTimes;
  const usedTimes = apiMembership ? apiMembership.freezeCountUsed : localMs!.freezeCountUsed;
  const maxDays = apiMembership ? apiMembership.maxFreezeDays : localPlan!.maxFreezeDays;
  const usedDays = apiMembership ? apiMembership.freezeDaysUsed : localMs!.freezeDaysUsed;
  const remainDays = Math.max(0, maxDays - usedDays);

  const [start, setStart] = useState(addDays(todayStr(), 1));
  const [days, setDays] = useState(Math.min(7, Math.max(1, remainDays)));
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (apiMembership) {
      setBusy(true);
      try {
        await membershipPlansApi.createFreezeRequest({
          membershipId: apiMembership.id,
          fromDate: `${start}T00:00:00Z`,
          days,
          reason: reason.trim(),
        });
        toast('Đã gửi yêu cầu freeze, vui lòng chờ lễ tân duyệt', 'success');
        await onSaved();
        onClose();
      } catch (err) {
        toast((err as Error).message, 'error');
      } finally {
        setBusy(false);
      }
      return;
    }

    const ok = act((d) => L.requestFreeze(d, user!.id, localMs!.id, start, days, reason), 'Đã gửi yêu cầu freeze, vui lòng chờ lễ tân duyệt');
    if (ok !== undefined) onClose();
  };

  return (
    <Modal
      title={<span className="row"><Snowflake size={20} color="#7cc4ff" /> Yêu cầu tạm ngưng gói</span>}
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose} disabled={busy}>Hủy</button>
          <button className="btn primary" onClick={submit} disabled={!reason.trim() || remainDays <= 0 || busy} id="freeze-submit">
            {busy ? 'Đang gửi...' : 'Gửi yêu cầu'}
          </button>
        </>
      }
    >
      <div className="grid g3 mb16" style={{ gap: 10 }}>
        <div className="card" style={{ padding: 14 }}><div className="small muted">Lần còn lại</div><b style={{ fontSize: 20 }}>{Math.max(0, maxTimes - usedTimes)}</b></div>
        <div className="card" style={{ padding: 14 }}><div className="small muted">Ngày còn lại</div><b style={{ fontSize: 20 }}>{remainDays}</b></div>
        <div className="card" style={{ padding: 14 }}><div className="small muted">Hạn mới dự kiến</div><b style={{ fontSize: 16 }}>{fmtDate(addDays(endDateStr, days))}</b></div>
      </div>
      <div className="form-grid">
        <div className="field"><label>Bắt đầu từ</label><input type="date" className="input" min={addDays(todayStr(), 1)} max={endDateStr} value={start} onChange={(e) => setStart(e.target.value)} id="freeze-start" /></div>
        <div className="field"><label>Số ngày (tối đa {remainDays})</label><input type="number" className="input" min={1} max={Math.max(1, remainDays)} value={days} onChange={(e) => setDays(+e.target.value)} id="freeze-days" /></div>
        <div className="field full"><label>Lý do</label><textarea className="input" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="VD: Đi công tác, ốm..." id="freeze-reason" /></div>
      </div>
      <div className="small muted mt16">⚠️ Khi được duyệt, các lịch lớp/PT trong khoảng {fmtDate(start)} → {fmtDate(addDays(start, Math.max(0, days - 1)))} sẽ tự động bị hủy và hoàn lượt/buổi PT.</div>
    </Modal>
  );
}

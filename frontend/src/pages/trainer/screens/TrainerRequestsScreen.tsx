import { useEffect, useState, useCallback } from 'react';
import { Check, X, RefreshCw } from 'lucide-react';
import { useApp } from '../../../store/AppContext';
import * as L from '../../../store/logic';
import { Avatar, Badge, Empty, PageHead, Tabs, ReasonModal } from '../../../components/ui';
import { ptbLabel } from '../../../components/labels';
import { fmtDateTime, fmtTime, fmtWeekday } from '../../../utils';
import { ptApi, type ApiTrainerPtRequestDto } from '../../../api/ptApi';
import { emitAppDataChanged } from '../../../api/notificationsApi';
import { isApiNetworkError } from '../../../api/apiClient';

export function TrainerRequestsScreen() {
  const { db, user, act, toast } = useApp();
  const [tab, setTab] = useState<'pending' | 'confirmed' | 'history'>('pending');
  const [reject, setReject] = useState<string | null>(null);
  const [cancel, setCancel] = useState<string | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [apiRequests, setApiRequests] = useState<ApiTrainerPtRequestDto[] | null>(null);

  const fetchRequests = useCallback(async () => {
    setLoading(true);
    try {
      const data = await ptApi.getTrainerPtRequests();
      setApiRequests(data);
    } catch (err) {
      if (!isApiNetworkError(err)) {
        console.warn('Lỗi tải yêu cầu PT từ API, dùng dữ liệu local:', err);
      }
      setApiRequests(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchRequests();
  }, [fetchRequests]);

  const now = Date.now();

  // Combine or fallback to local db
  const localAll = db.ptBookings.filter((b) => b.trainerId === user!.id);

  const lists = {
    pending: apiRequests
      ? apiRequests.filter((b) => b.status.toLowerCase() === 'pending')
      : localAll.filter((b) => b.status === 'pending').sort((a, b) => a.startAt.localeCompare(b.startAt)),
    confirmed: apiRequests
      ? apiRequests.filter((b) => b.status.toLowerCase() === 'confirmed')
      : localAll.filter((b) => b.status === 'confirmed').sort((a, b) => a.startAt.localeCompare(b.startAt)),
    history: apiRequests
      ? apiRequests.filter((b) => !['pending', 'confirmed'].includes(b.status.toLowerCase()))
      : localAll.filter((b) => !['pending', 'confirmed'].includes(b.status)).sort((a, b) => b.startAt.localeCompare(a.startAt)),
  };

  const handleRespond = async (bookingId: string, accept: boolean, reason?: string) => {
    try {
      await ptApi.respondPtRequest(bookingId, accept, reason);
      toast(accept ? 'Đã xác nhận lịch hẹn' : 'Đã từ chối và hoàn buổi cho hội viên', 'success');
      await fetchRequests();
      emitAppDataChanged();
    } catch (err) {
      if (isApiNetworkError(err)) {
        act((d) => L.respondPT(d, bookingId, accept, reason), accept ? 'Đã xác nhận lịch hẹn' : 'Đã từ chối và hoàn buổi cho hội viên');
      } else {
        toast((err as Error).message, 'error');
      }
    }
  };

  const handleComplete = async (bookingId: string, attended: boolean) => {
    try {
      await ptApi.completePtSession(bookingId, attended);
      toast(attended ? 'Đã xác nhận hoàn thành buổi tập' : 'Đã ghi nhận vắng mặt', 'success');
      await fetchRequests();
      emitAppDataChanged();
    } catch (err) {
      if (isApiNetworkError(err)) {
        act((d) => L.completePT(d, bookingId, attended), attended ? 'Đã xác nhận hoàn thành buổi tập' : 'Đã ghi nhận vắng mặt');
      } else {
        toast((err as Error).message, 'error');
      }
    }
  };

  const handleCancelByTrainer = async (bookingId: string, reason: string) => {
    try {
      await ptApi.cancelPtSessionByTrainer(bookingId, reason);
      toast('Đã hủy lịch và hoàn buổi cho hội viên', 'success');
      await fetchRequests();
      emitAppDataChanged();
    } catch (err) {
      if (isApiNetworkError(err)) {
        act((d) => L.cancelPTByTrainer(d, bookingId, reason), 'Đã hủy lịch và hoàn buổi cho hội viên');
      } else {
        toast((err as Error).message, 'error');
      }
    }
  };

  return (
    <>
      <PageHead
        title="Lịch hẹn 1-kèm-1"
        desc="Xác nhận/từ chối yêu cầu, xác nhận buổi tập đã diễn ra (yêu cầu hội viên check-in hợp lệ)."
        actions={
          <button className="btn ghost sm" onClick={fetchRequests} disabled={loading}>
            <RefreshCw size={14} className={loading ? 'spin' : ''} /> Làm mới
          </button>
        }
      />
      <Tabs
        value={tab}
        onChange={setTab}
        tabs={[
          { id: 'pending', label: 'Chờ xác nhận', count: lists.pending.length },
          { id: 'confirmed', label: 'Đã xác nhận', count: lists.confirmed.length },
          { id: 'history', label: 'Lịch sử' },
        ]}
      />
      {lists[tab].length === 0 ? (
        <div className="card"><Empty text="Không có lịch hẹn" /></div>
      ) : (
        <div className="col">
          {lists[tab].map((b) => {
            const memberName = 'memberName' in b ? b.memberName : L.userById(db, b.memberId)?.fullName;
            const memberPhone = 'memberPhone' in b ? b.memberPhone : L.userById(db, b.memberId)?.phone;
            const branchName = 'branchName' in b ? b.branchName : L.branchById(db, b.branchId)?.name;
            const sourceType = 'sourceType' in b ? b.sourceType : (b.source?.type ?? 'membership_free');
            const started = new Date(b.startAt).getTime() < now;
            const checked = b.checkedIn;
            const statusKey = b.status.toLowerCase() as keyof typeof ptbLabel;

            return (
              <div className="list-item" key={b.id}>
                <Avatar user={{ fullName: memberName || 'Hội viên', avatarColor: '#4f46e5' }} />
                <div className="grow" style={{ minWidth: 0 }}>
                  <div className="row wrap">
                    <b>{memberName}</b>
                    <span className="small dim">{memberPhone}</span>
                  </div>
                  <div className="small muted">
                    {fmtWeekday(b.startAt)} · {fmtDateTime(b.startAt)} – {fmtTime(b.endAt)} · {branchName}
                  </div>
                  {b.note && <div className="small" style={{ color: '#b8a6ff' }}>“{b.note}”</div>}
                </div>
                <Badge color={sourceType === 'membership_free' ? 'pink' : 'blue'} plain>
                  {sourceType === 'membership_free' ? 'Buổi tặng' : 'Gói PT'}
                </Badge>
                {tab === 'pending' && (
                  <>
                    <button className="btn sm danger" onClick={() => setReject(b.id)} id={`reject-${b.id}`}>
                      <X size={14} /> Từ chối
                    </button>
                    <button className="btn sm success" onClick={() => handleRespond(b.id, true)} id={`accept-${b.id}`}>
                      <Check size={14} /> Xác nhận
                    </button>
                  </>
                )}
                {tab === 'confirmed' && (
                  started ? (
                    <>
                      <Badge color={checked ? 'green' : 'gray'}>{checked ? 'Đã check-in' : 'Chưa check-in'}</Badge>
                      <button className="btn sm" onClick={() => handleComplete(b.id, false)}>Vắng mặt</button>
                      <button className="btn sm success" onClick={() => handleComplete(b.id, true)} id={`complete-${b.id}`}>
                        <Check size={14} /> Hoàn thành
                      </button>
                    </>
                  ) : (
                    <button className="btn sm danger" onClick={() => setCancel(b.id)}>
                      <X size={14} /> Hủy lịch
                    </button>
                  )
                )}
                {tab === 'history' && (
                  <Badge color={ptbLabel[statusKey]?.[1] ?? 'gray'}>
                    {ptbLabel[statusKey]?.[0] ?? b.status}
                  </Badge>
                )}
              </div>
            );
          })}
        </div>
      )}
      {reject && (
        <ReasonModal
          title="Từ chối lịch hẹn"
          label="Lý do (gửi đến hội viên)"
          confirmText="Từ chối"
          danger
          onClose={() => setReject(null)}
          onConfirm={(r) => {
            handleRespond(reject, false, r);
            setReject(null);
          }}
        />
      )}
      {cancel && (
        <ReasonModal
          title="Hủy lịch hẹn đã xác nhận"
          label="Lý do"
          confirmText="Hủy lịch"
          danger
          required
          onClose={() => setCancel(null)}
          onConfirm={(r) => {
            handleCancelByTrainer(cancel, r);
            setCancel(null);
          }}
        />
      )}
    </>
  );
}

export default TrainerRequestsScreen;

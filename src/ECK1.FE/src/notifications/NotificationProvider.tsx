import {
  createContext,
  useContext,
  useState,
  useCallback,
  type ReactNode,
} from 'react';
import {
  Snackbar,
  Alert,
  AlertTitle,
  Button,
} from '@mui/material';
import { useNavigate } from 'react-router-dom';

export interface NotificationAction {
  label: string;
  href: string;
}

export interface NotificationOptions {
  title?: string;
  message: string;
  severity?: 'success' | 'info' | 'warning' | 'error';
  duration?: number;
  action?: NotificationAction;
}

interface NotificationEntry extends NotificationOptions {
  id: number;
}

type ShowNotificationFn = (opts: NotificationOptions) => void;

export type ShowNotification = ShowNotificationFn;

const NotificationContext = createContext<ShowNotificationFn>(() => {});

export function useNotification(): ShowNotification {
  return useContext(NotificationContext);
}

let nextId = 0;

export function NotificationProvider({ children }: { children: ReactNode }) {
  const [queue, setQueue] = useState<NotificationEntry[]>([]);
  const navigate = useNavigate();

  const show = useCallback((opts: NotificationOptions) => {
    const entry: NotificationEntry = { ...opts, id: ++nextId };
    setQueue((prev) => [...prev, entry]);
  }, []);

  const dismiss = useCallback((id: number) => {
    setQueue((prev) => prev.filter((n) => n.id !== id));
  }, []);

  const current = queue[0];

  return (
    <NotificationContext.Provider value={show}>
      {children}
      {current && (
        <Snackbar
          key={current.id}
          open
          autoHideDuration={current.duration ?? 5000}
          onClose={(_, reason) => {
            if (reason === 'clickaway') return;
            dismiss(current.id);
          }}
          anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        >
          <Alert
            severity={current.severity ?? 'info'}
            variant="filled"
            onClose={() => dismiss(current.id)}
            action={
              current.action ? (
                <Button
                  color="inherit"
                  size="small"
                  onClick={() => {
                    navigate(current.action!.href);
                    dismiss(current.id);
                  }}
                >
                  {current.action.label}
                </Button>
              ) : undefined
            }
          >
            {current.title && <AlertTitle>{current.title}</AlertTitle>}
            {current.message}
          </Alert>
        </Snackbar>
      )}
    </NotificationContext.Provider>
  );
}

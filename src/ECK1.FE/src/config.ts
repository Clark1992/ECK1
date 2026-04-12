interface AppConfig {
  ZITADEL_AUTHORITY: string;
  ZITADEL_CLIENT_ID: string;
}

declare global {
  interface Window {
    __CONFIG__?: Partial<AppConfig>;
  }
}

function env(key: keyof AppConfig): string {
  const value = window.__CONFIG__?.[key] ?? import.meta.env[`VITE_${key}`];
  if (!value) {
    throw new Error(`Missing required config: ${key}. Set it via window.__CONFIG__ or VITE_${key} env var.`);
  }
  return value;
}

export const config: AppConfig = {
  ZITADEL_AUTHORITY: env('ZITADEL_AUTHORITY'),
  ZITADEL_CLIENT_ID: env('ZITADEL_CLIENT_ID'),
};

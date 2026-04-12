/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_ZITADEL_AUTHORITY: string;
  readonly VITE_ZITADEL_CLIENT_ID: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

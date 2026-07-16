/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Where the API lives. */
  readonly VITE_API_BASE_URL?: string

  /** The lab this stubbed session acts as; sent as X-Lab-Id. */
  readonly VITE_LAB_ID?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

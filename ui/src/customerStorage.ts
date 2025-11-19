export type CustomerDraft = {
  CustomerName: string;
  CustomerEmail: string;
  CustomerHomePhone: string;
  CustomerWorkPhone: string;
  CustomerStreet: string;
  CustomerCity: string;
  CustomerState: string;
  CustomerZip: string;
  CustomerSignatureDataUrl: string;
};

export const defaultCustomer: CustomerDraft = {
  CustomerName: '',
  CustomerEmail: '',
  CustomerHomePhone: '',
  CustomerWorkPhone: '',
  CustomerStreet: '',
  CustomerCity: '',
  CustomerState: '',
  CustomerZip: '',
  CustomerSignatureDataUrl: ''
};

/**
 * Toggle this flag to quickly inject canned customer details for manual testing.
 * Set to `true` to have the UI prefill the Customer Details panel with the values below.
 */
export const ENABLE_CUSTOMER_TEST_DATA = true;

export const TEST_CUSTOMER_DATA: CustomerDraft = {
  CustomerName: 'Ava Thompson',
  CustomerEmail: 'ava@example.com',
  CustomerHomePhone: '555-111-2222',
  CustomerWorkPhone: '555-333-4444',
  CustomerStreet: '123 Market Street',
  CustomerCity: 'Los Angeles',
  CustomerState: 'CA',
  CustomerZip: '90001',
  CustomerSignatureDataUrl: ''
};

const STORAGE_KEY = 'idsforms.customer';

const legacyKeyMap: Record<string, keyof CustomerDraft> = {
  name: 'CustomerName',
  email: 'CustomerEmail',
  homePhone: 'CustomerHomePhone',
  workPhone: 'CustomerWorkPhone',
  street: 'CustomerStreet',
  city: 'CustomerCity',
  state: 'CustomerState',
  zip: 'CustomerZip'
};

function normalizeCustomer(raw: unknown): CustomerDraft {
  const next: CustomerDraft = { ...defaultCustomer };
  if (raw && typeof raw === 'object') {
    for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
      const targetKey = legacyKeyMap[key] || ((key in next ? key : undefined) as keyof CustomerDraft | undefined);
      if (!targetKey) continue;
      const str = typeof value === 'string' ? value : value == null ? '' : String(value);
      next[targetKey] = str;
    }
  }
  return next;
}

export function readCustomerDraft(): CustomerDraft {
  const fallback = ENABLE_CUSTOMER_TEST_DATA
    ? { ...defaultCustomer, ...TEST_CUSTOMER_DATA }
    : { ...defaultCustomer };
  if (typeof window === 'undefined') return fallback;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (raw) {
      return normalizeCustomer(JSON.parse(raw));
    }
    return fallback;
  } catch {
    return fallback;
  }
}

export function writeCustomerDraft(data: CustomerDraft): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
  } catch {
    // ignore storage failures
  }
}

export function customerToPrefillPayload(data?: Partial<CustomerDraft>): Record<string, string> {
  const payload: Record<string, string> = {};
  if (!data) return payload;
  for (const [key, value] of Object.entries(data)) {
    if (typeof value === 'string' && value.trim()) {
      payload[key] = value;
    }
  }
  return payload;
}

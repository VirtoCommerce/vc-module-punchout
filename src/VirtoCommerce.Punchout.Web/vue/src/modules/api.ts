// Punchout user mappings API (PunchoutUserMappingController), same-origin cookie auth like the generated clients.

export interface PunchoutUserMapping {
  id?: string;
  isActive?: boolean;
  externalId?: string;
  userId?: string;
  userName?: string;
  memberId?: string;
}

export interface SecurityAccount {
  id?: string;
  userName?: string;
}

/** The contact a mapping belongs to (Customer module's /api/members) */
export interface Member {
  id?: string;
  name?: string;
  securityAccounts?: SecurityAccount[];
}

/** A failed call; `body` is the parsed response ({ errorCode, errorParameters } for a validation error). */
export class ApiError extends Error {
  constructor(
    public status: number,
    public body: unknown,
  ) {
    super(typeof body === "string" && body ? body : `Error ${status}`);
  }
}

async function http<T>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: { Accept: "application/json", ...(body !== undefined && { "Content-Type": "application/json" }) },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let data: unknown = text;
  try {
    data = text ? JSON.parse(text) : undefined;
  } catch {
    // plain-text body
  }
  if (!res.ok) throw new ApiError(res.status, data);
  return data as T;
}

const BASE = "/api/punchout-user-mappings";

export const userMappings = {
  // one contact has one mapping
  byMember: (memberId: string) =>
    http<{ results?: PunchoutUserMapping[] }>("POST", `${BASE}/search`, { memberIds: [memberId], take: 1 }).then(
      (r) => r.results?.[0],
    ),
  getNew: () => http<PunchoutUserMapping>("GET", `${BASE}/new`),
  create: (m: PunchoutUserMapping) => http<PunchoutUserMapping>("POST", BASE, m),
  update: (m: PunchoutUserMapping) => http<PunchoutUserMapping>("PUT", BASE, m),
  delete: (id: string) => http<void>("DELETE", `${BASE}?ids=${encodeURIComponent(id)}`),
};

export const getMember = (id: string) => http<Member>("GET", `/api/members/${encodeURIComponent(id)}`);

export interface FieldError {
  path: string
  message: string
}

export interface ApiErrorBody {
  error: string
  message: string
  errors?: FieldError[] | null
  filterErrors?: FieldError[] | null
  templateErrors?: FieldError[] | null
  retryAfterSeconds?: number | null
}

export interface FeedHealth {
  consecutiveFailures: number
  lastSuccessAt: string | null
  lastErrorAt: string | null
  lastError: string | null
}

export interface LatestItem {
  id: string
  title: string | null
  link: string | null
  published: string | null
  fetchedAt: string
}

export type FilterField = 'title' | 'description' | 'author' | 'link' | 'category'
export type FilterOperator = 'contains' | 'equals' | 'startsWith' | 'regex'

export interface FilterGroup {
  type: 'all' | 'any'
  negate?: boolean
  children: FilterNode[]
}

export interface FilterCondition {
  type: 'condition'
  negate?: boolean
  field: FilterField
  domain?: string
  operator: FilterOperator
  value: string
  caseSensitive?: boolean
}

export type FilterNode = FilterGroup | FilterCondition

export interface EmbedTemplate {
  title?: string
  description?: string
  url?: string
  color?: number
  image?: string
  thumbnail?: string
  footer?: string
  author?: string
  timestamp?: boolean
}

export interface MessageTemplate {
  content?: string
  embed?: EmbedTemplate
}

export interface Destination {
  sink: string
  parameters: Record<string, string>
}

export interface DestinationState extends Destination {
  brokenSince: string | null
  brokenReason: string | null
}

export interface Route {
  id: number
  feedId: number
  name: string
  enabled: boolean
  filter: FilterNode | null
  template: MessageTemplate | null
  destinations: DestinationState[]
}

export interface RouteInput {
  name: string
  enabled: boolean
  filter: FilterNode | null
  template: MessageTemplate | null
  destinations: Destination[]
}

export interface RouteValidation {
  valid: boolean
  errors: FieldError[]
  filterErrors: FieldError[]
  templateErrors: FieldError[]
}

export interface Feed {
  id: number
  url: string
  name: string
  intervalSeconds: number
  health: FeedHealth
  latestItem: LatestItem | null
  nextFetchAt: string | null
  routes: Route[]
}

export interface FeedInput {
  url: string
  name: string
  intervalSeconds: number
}

export interface FeedCategory {
  domain: string | null
  value: string
}

export interface FeedItemPreview {
  id: string
  title: string | null
  link: string | null
  author: string | null
  published: string | null
  categories: FeedCategory[]
  imageUrl: string | null
}

export interface FeedValidation {
  url: string
  title: string | null
  itemCount: number
  items: FeedItemPreview[]
}

export interface Setting {
  key: string
  label: string
  description: string
  group: string
  placeholder: string | null
  secret: boolean
  isSet: boolean
  value: string | null
  defined: boolean
}

export interface Meta {
  version: string
  sinks: string[]
  placeholders: string[]
  defaultTemplateContent: string
  minIntervalSeconds: number
  defaultIntervalSeconds: number
  maxIntervalSeconds: number
}

export interface Guild {
  id: string
  name: string
  iconUrl: string | null
}

export interface Channel {
  id: string
  name: string
  type: 'text' | 'announcement' | 'forum'
  categoryId: string | null
  categoryName: string | null
  position: number
}

export interface PermissionCheck {
  name: string
  label: string
  granted: boolean
}

export interface ChannelPermissions {
  guildId: string
  channelId: string
  channelType: string
  canPost: boolean
  permissions: PermissionCheck[]
}

/** An API call that answered with an error status; `body` holds the JSON error when the server sent one. */
export class ApiRequestError extends Error {
  readonly status: number
  readonly body: ApiErrorBody | null

  constructor(status: number, body: ApiErrorBody | null) {
    super(body?.message ?? `Request failed with HTTP ${status}`)
    this.status = status
    this.body = body
  }
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(path, {
    method,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()
  const json: unknown = text.length > 0 && response.headers.get('Content-Type')?.includes('json') ? JSON.parse(text) : null
  if (!response.ok) {
    throw new ApiRequestError(response.status, (json as ApiErrorBody | null) ?? null)
  }
  return json as T
}

const id = (value: string) => encodeURIComponent(value)

export const api = {
  meta: () => request<Meta>('GET', '/api/meta'),

  feeds: () => request<Feed[]>('GET', '/api/feeds'),
  feed: (feedId: number) => request<Feed>('GET', `/api/feeds/${feedId}`),
  createFeed: (input: FeedInput) => request<Feed>('POST', '/api/feeds', input),
  updateFeed: (feedId: number, input: FeedInput) => request<Feed>('PUT', `/api/feeds/${feedId}`, input),
  deleteFeed: (feedId: number) => request<null>('DELETE', `/api/feeds/${feedId}`),
  validateFeed: (url: string) => request<FeedValidation>('POST', '/api/feeds/validate', { url }),

  route: (routeId: number) => request<Route>('GET', `/api/routes/${routeId}`),
  createRoute: (feedId: number, input: RouteInput) => request<Route>('POST', `/api/feeds/${feedId}/routes`, input),
  updateRoute: (routeId: number, input: RouteInput) => request<Route>('PUT', `/api/routes/${routeId}`, input),
  deleteRoute: (routeId: number) => request<null>('DELETE', `/api/routes/${routeId}`),
  validateRoute: (input: RouteInput) => request<RouteValidation>('POST', '/api/routes/validate', input),

  settings: () => request<Setting[]>('GET', '/api/settings'),
  updateSettings: (changes: Record<string, string | null>) => request<Setting[]>('PUT', '/api/settings', changes),

  guilds: (refresh = false) => request<Guild[]>('GET', `/api/discord/guilds${refresh ? '?refresh=true' : ''}`),
  channels: (guildId: string, refresh = false) =>
    request<Channel[]>('GET', `/api/discord/guilds/${id(guildId)}/channels${refresh ? '?refresh=true' : ''}`),
  permissions: (guildId: string, channelId: string) =>
    request<ChannelPermissions>('GET', `/api/discord/guilds/${id(guildId)}/channels/${id(channelId)}/permissions`),
  testMessage: (channelId: string, template: MessageTemplate | null) =>
    request<{ channelId: string; messageId: string | null }>('POST', `/api/discord/channels/${id(channelId)}/test-message`, {
      templateJson: template === null ? null : JSON.stringify(template),
    }),
}

/** A readable message for any error thrown by an API call. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiRequestError) {
    return error.message
  }
  return error instanceof Error ? error.message : String(error)
}

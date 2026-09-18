<script setup lang="ts">
import axios from 'axios'
import { computed, onMounted, reactive, ref } from 'vue'
import api from './services/api'
import type { AuthResponse, AuthUser } from './types/auth'
import type {
  ApiProblem,
  PagedResult,
  TaskItem,
  TaskPriority,
  TaskStatus,
  TaskSummary,
  TaskWriteRequest,
} from './types/task'

type AuthMode = 'login' | 'register'

const currentUser = ref<AuthUser | null>(null)
const checkingAuth = ref(true)
const authMode = ref<AuthMode>('login')
const authSaving = ref(false)
const authError = ref('')
const authForm = reactive({
  email: '',
  password: '',
  confirmPassword: '',
})

const tasks = ref<TaskItem[]>([])
const summary = reactive<TaskSummary>({
  totalCount: 0,
  todoCount: 0,
  inProgressCount: 0,
  doneCount: 0,
  archivedCount: 0,
  overdueCount: 0,
  dueTodayCount: 0,
})

const paging = reactive({ page: 1, pageSize: 6, totalCount: 0, totalPages: 0 })
const filters = reactive({
  search: '',
  status: '' as '' | TaskStatus,
  priority: '' as '' | TaskPriority,
  sortBy: 'CreatedAt',
  sortDirection: 'desc',
})

const draft = reactive({
  title: '',
  description: '',
  priority: 'Medium' as TaskPriority,
  dueDate: '',
})

const edit = reactive({
  id: null as number | null,
  title: '',
  description: '',
  status: 'Todo' as TaskStatus,
  priority: 'Medium' as TaskPriority,
  dueDate: '',
})

const loading = ref(false)
const saving = ref(false)
const errorMessage = ref('')

const pageLabel = computed(() => {
  if (paging.totalPages === 0) return 'Page 0 of 0'
  return `Page ${paging.page} of ${paging.totalPages}`
})

const authHeading = computed(() =>
  authMode.value === 'login' ? 'Welcome back' : 'Create your workspace',
)

function toUtcEndOfDay(date: string): string | null {
  return date ? `${date}T23:59:59Z` : null
}

function toDateInput(value: string | null): string {
  return value?.slice(0, 10) ?? ''
}

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleDateString() : 'No due date'
}

function readApiError(error: unknown, fallback: string): string {
  if (!axios.isAxiosError<ApiProblem>(error)) return fallback

  const problem = error.response?.data
  const validationMessage = problem?.errors
    ? Object.values(problem.errors).flat().join(' ')
    : null

  const message = validationMessage || problem?.detail || problem?.title || fallback
  return problem?.traceId ? `${message} (traceId: ${problem.traceId})` : message
}

function isUnauthorized(error: unknown): boolean {
  return axios.isAxiosError(error) && error.response?.status === 401
}

function resetDashboard(): void {
  tasks.value = []
  Object.assign(summary, {
    totalCount: 0,
    todoCount: 0,
    inProgressCount: 0,
    doneCount: 0,
    archivedCount: 0,
    overdueCount: 0,
    dueTodayCount: 0,
  })
  Object.assign(paging, { page: 1, totalCount: 0, totalPages: 0 })
  Object.assign(filters, {
    search: '',
    status: '',
    priority: '',
    sortBy: 'CreatedAt',
    sortDirection: 'desc',
  })
  Object.assign(draft, {
    title: '',
    description: '',
    priority: 'Medium',
    dueDate: '',
  })
  edit.id = null
}

function expireSession(): void {
  currentUser.value = null
  resetDashboard()
  authMode.value = 'login'
  authError.value = 'Your session expired. Please sign in again.'
}

function handleDashboardError(error: unknown, fallback: string): void {
  if (isUnauthorized(error)) {
    expireSession()
    return
  }

  errorMessage.value = readApiError(error, fallback)
}

function switchAuthMode(mode: AuthMode): void {
  authMode.value = mode
  authError.value = ''
  authForm.password = ''
  authForm.confirmPassword = ''
}

async function submitAuth(): Promise<void> {
  authError.value = ''

  if (authMode.value === 'register' && authForm.password !== authForm.confirmPassword) {
    authError.value = 'Passwords do not match.'
    return
  }

  authSaving.value = true
  try {
    const endpoint = authMode.value === 'login' ? '/auth/login' : '/auth/register'
    const response = await api.post<AuthResponse>(endpoint, {
      email: authForm.email.trim(),
      password: authForm.password,
    })

    currentUser.value = response.data.user
    authForm.password = ''
    authForm.confirmPassword = ''
    resetDashboard()
    await loadDashboard()
  } catch (error) {
    authError.value = readApiError(error, 'Could not sign in. Please try again.')
  } finally {
    authSaving.value = false
  }
}

async function restoreSession(): Promise<void> {
  try {
    const response = await api.get<AuthUser>('/auth/me')
    currentUser.value = response.data
    await loadDashboard()
  } catch (error) {
    if (!isUnauthorized(error)) {
      authError.value = readApiError(error, 'Could not connect to the API.')
    }
  } finally {
    checkingAuth.value = false
  }
}

async function logout(): Promise<void> {
  try {
    await api.post('/auth/logout')
  } catch (error) {
    if (!isUnauthorized(error)) {
      errorMessage.value = readApiError(error, 'Could not complete logout.')
      return
    }
  }

  currentUser.value = null
  resetDashboard()
  authMode.value = 'login'
  authError.value = ''
}

async function loadDashboard(): Promise<void> {
  loading.value = true
  errorMessage.value = ''

  try {
    const [tasksResponse, summaryResponse] = await Promise.all([
      api.get<PagedResult<TaskItem>>('/tasks', {
        params: {
          page: paging.page,
          pageSize: paging.pageSize,
          search: filters.search.trim() || undefined,
          status: filters.status || undefined,
          priority: filters.priority || undefined,
          sortBy: filters.sortBy,
          sortDirection: filters.sortDirection,
        },
      }),
      api.get<TaskSummary>('/tasks/summary'),
    ])

    tasks.value = tasksResponse.data.items
    Object.assign(paging, {
      page: tasksResponse.data.page,
      pageSize: tasksResponse.data.pageSize,
      totalCount: tasksResponse.data.totalCount,
      totalPages: tasksResponse.data.totalPages,
    })
    Object.assign(summary, summaryResponse.data)
  } catch (error) {
    handleDashboardError(error, 'Could not load tasks.')
  } finally {
    loading.value = false
  }
}

async function createTask(): Promise<void> {
  if (!draft.title.trim()) {
    errorMessage.value = 'Title is required.'
    return
  }

  saving.value = true
  errorMessage.value = ''

  const payload: TaskWriteRequest = {
    title: draft.title.trim(),
    description: draft.description.trim() || null,
    isCompleted: false,
    status: 'Todo',
    priority: draft.priority,
    dueDate: toUtcEndOfDay(draft.dueDate),
  }

  try {
    await api.post('/tasks', payload)
    Object.assign(draft, {
      title: '',
      description: '',
      priority: 'Medium',
      dueDate: '',
    })
    paging.page = 1
    await loadDashboard()
  } catch (error) {
    handleDashboardError(error, 'Could not create the task.')
  } finally {
    saving.value = false
  }
}

function startEdit(task: TaskItem): void {
  Object.assign(edit, {
    id: task.id,
    title: task.title,
    description: task.description ?? '',
    status: task.status,
    priority: task.priority,
    dueDate: toDateInput(task.dueDate),
  })
}

function cancelEdit(): void {
  edit.id = null
}

async function saveEdit(task: TaskItem): Promise<void> {
  if (!edit.title.trim()) {
    errorMessage.value = 'Title is required.'
    return
  }

  saving.value = true
  errorMessage.value = ''

  const payload: TaskWriteRequest = {
    title: edit.title.trim(),
    description: edit.description.trim() || null,
    isCompleted: edit.status === 'Done',
    status: edit.status,
    priority: edit.priority,
    dueDate: toUtcEndOfDay(edit.dueDate),
  }

  try {
    await api.put(`/tasks/${task.id}`, payload)
    edit.id = null
    await loadDashboard()
  } catch (error) {
    handleDashboardError(error, 'Could not update the task.')
  } finally {
    saving.value = false
  }
}

async function toggleComplete(task: TaskItem): Promise<void> {
  const status: TaskStatus = task.status === 'Done' ? 'Todo' : 'Done'
  errorMessage.value = ''

  try {
    await api.patch(`/tasks/${task.id}/status`, { status })
    await loadDashboard()
  } catch (error) {
    handleDashboardError(error, 'Could not change the task status.')
  }
}

async function deleteTask(task: TaskItem): Promise<void> {
  if (!window.confirm(`Delete “${task.title}”?`)) return

  errorMessage.value = ''
  try {
    await api.delete(`/tasks/${task.id}`)
    if (tasks.value.length === 1 && paging.page > 1) paging.page -= 1
    await loadDashboard()
  } catch (error) {
    handleDashboardError(error, 'Could not delete the task.')
  }
}

async function applyFilters(): Promise<void> {
  paging.page = 1
  await loadDashboard()
}

async function changePage(page: number): Promise<void> {
  if (page < 1 || page > paging.totalPages || page === paging.page) return
  paging.page = page
  await loadDashboard()
}

onMounted(restoreSession)
</script>

<template>
  <main v-if="checkingAuth" class="auth-shell">
    <section class="auth-card state" aria-live="polite">Checking your session…</section>
  </main>

  <main v-else-if="!currentUser" class="auth-shell">
    <section class="auth-card" aria-labelledby="auth-heading">
      <div class="auth-intro">
        <p class="eyebrow">ASP.NET Core + Vue 3</p>
        <h1 id="auth-heading">{{ authHeading }}</h1>
        <p>
          Sign in to manage your private task workspace. Each account can only access its own data.
        </p>
      </div>

      <div class="auth-tabs" role="tablist" aria-label="Authentication mode">
        <button
          type="button"
          role="tab"
          :aria-selected="authMode === 'login'"
          :class="{ active: authMode === 'login' }"
          @click="switchAuthMode('login')"
        >
          Sign in
        </button>
        <button
          type="button"
          role="tab"
          :aria-selected="authMode === 'register'"
          :class="{ active: authMode === 'register' }"
          @click="switchAuthMode('register')"
        >
          Register
        </button>
      </div>

      <p v-if="authError" class="error-banner" role="alert">{{ authError }}</p>

      <form class="auth-form" @submit.prevent="submitAuth">
        <label>
          Email
          <input
            v-model="authForm.email"
            type="email"
            autocomplete="email"
            maxlength="254"
            required
            placeholder="you@example.com"
          />
        </label>
        <label>
          Password
          <input
            v-model="authForm.password"
            type="password"
            :autocomplete="authMode === 'login' ? 'current-password' : 'new-password'"
            minlength="8"
            maxlength="128"
            required
          />
        </label>
        <label v-if="authMode === 'register'">
          Confirm password
          <input
            v-model="authForm.confirmPassword"
            type="password"
            autocomplete="new-password"
            minlength="8"
            maxlength="128"
            required
          />
        </label>
        <button class="primary" type="submit" :disabled="authSaving">
          {{ authSaving ? 'Please wait…' : authMode === 'login' ? 'Sign in' : 'Create account' }}
        </button>
      </form>
    </section>
  </main>

  <main v-else class="shell">
    <div class="account-bar">
      <span>Signed in as <strong>{{ currentUser.email }}</strong></span>
      <button type="button" @click="logout">Sign out</button>
    </div>

    <header class="hero">
      <div>
        <p class="eyebrow">ASP.NET Core + Vue 3</p>
        <h1>Task Management System</h1>
        <p class="subtitle">Track priorities, deadlines, and delivery status from one dashboard.</p>
      </div>
      <div class="summary-grid" aria-label="Task summary">
        <div><strong>{{ summary.totalCount }}</strong><span>Total</span></div>
        <div><strong>{{ summary.inProgressCount }}</strong><span>In progress</span></div>
        <div><strong>{{ summary.doneCount }}</strong><span>Done</span></div>
        <div class="danger"><strong>{{ summary.overdueCount }}</strong><span>Overdue</span></div>
      </div>
    </header>

    <p v-if="errorMessage" class="error-banner" role="alert">{{ errorMessage }}</p>

    <section class="panel" aria-labelledby="create-heading">
      <div class="section-heading">
        <div>
          <p class="eyebrow">New item</p>
          <h2 id="create-heading">Create a task</h2>
        </div>
      </div>

      <form class="task-form" @submit.prevent="createTask">
        <label>
          Title
          <input v-model="draft.title" maxlength="200" required placeholder="Ship the API release" />
        </label>
        <label class="wide">
          Description
          <textarea v-model="draft.description" maxlength="2000" rows="3" placeholder="Optional context"></textarea>
        </label>
        <label>
          Priority
          <select v-model="draft.priority">
            <option>Low</option><option>Medium</option><option>High</option><option>Critical</option>
          </select>
        </label>
        <label>
          Due date
          <input v-model="draft.dueDate" type="date" />
        </label>
        <button class="primary" type="submit" :disabled="saving">
          {{ saving ? 'Saving…' : 'Add task' }}
        </button>
      </form>
    </section>

    <section class="panel" aria-labelledby="tasks-heading">
      <div class="section-heading">
        <div>
          <p class="eyebrow">Workspace</p>
          <h2 id="tasks-heading">Tasks</h2>
        </div>
        <span class="count">{{ paging.totalCount }} results</span>
      </div>

      <form class="filters" @submit.prevent="applyFilters">
        <input v-model="filters.search" maxlength="200" placeholder="Search title or description" aria-label="Search tasks" />
        <select v-model="filters.status" aria-label="Filter by status">
          <option value="">All statuses</option>
          <option>Todo</option><option>InProgress</option><option>Done</option><option>Archived</option>
        </select>
        <select v-model="filters.priority" aria-label="Filter by priority">
          <option value="">All priorities</option>
          <option>Low</option><option>Medium</option><option>High</option><option>Critical</option>
        </select>
        <select v-model="filters.sortBy" aria-label="Sort field">
          <option value="CreatedAt">Created</option>
          <option value="DueDate">Due date</option>
          <option value="Priority">Priority</option>
          <option value="Status">Status</option>
          <option value="Title">Title</option>
        </select>
        <select v-model="filters.sortDirection" aria-label="Sort direction">
          <option value="desc">Descending</option>
          <option value="asc">Ascending</option>
        </select>
        <button type="submit">Apply</button>
      </form>

      <div v-if="loading" class="state">Loading tasks…</div>
      <div v-else-if="tasks.length === 0" class="state">No tasks match these filters.</div>

      <div v-else class="task-list">
        <article v-for="task in tasks" :key="task.id" class="task-card">
          <form v-if="edit.id === task.id" class="edit-form" @submit.prevent="saveEdit(task)">
            <input v-model="edit.title" maxlength="200" required aria-label="Task title" />
            <textarea v-model="edit.description" maxlength="2000" rows="3" aria-label="Task description"></textarea>
            <div class="edit-fields">
              <select v-model="edit.status" aria-label="Task status">
                <option>Todo</option><option>InProgress</option><option>Done</option><option>Archived</option>
              </select>
              <select v-model="edit.priority" aria-label="Task priority">
                <option>Low</option><option>Medium</option><option>High</option><option>Critical</option>
              </select>
              <input v-model="edit.dueDate" type="date" aria-label="Task due date" />
            </div>
            <div class="actions">
              <button class="primary" type="submit" :disabled="saving">Save</button>
              <button type="button" @click="cancelEdit">Cancel</button>
            </div>
          </form>

          <template v-else>
            <div class="task-copy">
              <div class="badges">
                <span class="badge" :data-status="task.status">{{ task.status }}</span>
                <span class="badge priority" :data-priority="task.priority">{{ task.priority }}</span>
              </div>
              <h3 :class="{ done: task.isCompleted }">{{ task.title }}</h3>
              <p v-if="task.description">{{ task.description }}</p>
              <small>Due: {{ formatDate(task.dueDate) }}</small>
            </div>
            <div class="actions">
              <button type="button" @click="toggleComplete(task)">
                {{ task.isCompleted ? 'Reopen' : 'Complete' }}
              </button>
              <button type="button" @click="startEdit(task)">Edit</button>
              <button class="danger-button" type="button" @click="deleteTask(task)">Delete</button>
            </div>
          </template>
        </article>
      </div>

      <nav class="pagination" aria-label="Task pages">
        <button type="button" :disabled="paging.page <= 1" @click="changePage(paging.page - 1)">Previous</button>
        <span>{{ pageLabel }}</span>
        <button type="button" :disabled="paging.page >= paging.totalPages" @click="changePage(paging.page + 1)">Next</button>
      </nav>
    </section>
  </main>
</template>

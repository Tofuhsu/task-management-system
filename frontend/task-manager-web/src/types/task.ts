export type TaskStatus = 'Todo' | 'InProgress' | 'Done' | 'Archived'
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Critical'

export interface TaskItem {
  id: number
  title: string
  description: string | null
  isCompleted: boolean
  status: TaskStatus
  priority: TaskPriority
  dueDate: string | null
  createdAt: string
  updatedAt: string | null
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface TaskSummary {
  totalCount: number
  todoCount: number
  inProgressCount: number
  doneCount: number
  archivedCount: number
  overdueCount: number
  dueTodayCount: number
}

export interface TaskWriteRequest {
  title: string
  description: string | null
  isCompleted: boolean
  status: TaskStatus
  priority: TaskPriority
  dueDate: string | null
}

export interface ApiProblem {
  title?: string
  detail?: string
  traceId?: string
  errors?: Record<string, string[]>
}

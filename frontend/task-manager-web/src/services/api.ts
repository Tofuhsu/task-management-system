import axios from 'axios'

const defaultApiBaseUrl = import.meta.env.DEV
  ? 'http://localhost:5100/api'
  : '/api'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || defaultApiBaseUrl,
  withCredentials: true,
  headers: {
    Accept: 'application/json',
  },
  timeout: 10_000,
})

export default api

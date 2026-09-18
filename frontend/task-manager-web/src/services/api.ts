import axios from 'axios'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5100/api',
  withCredentials: true,
  headers: {
    Accept: 'application/json',
  },
  timeout: 10_000,
})

export default api

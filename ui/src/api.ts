import axios from "axios";

// Use Vite dev proxy in development: all requests go to /api
// Vite forwards /api -> https://localhost:5001 (see vite.config.js)
const baseURL = "/api";

// Helpful during setup: see which URL is used
if (typeof window !== "undefined") {
  // eslint-disable-next-line no-console
  console.log("API baseURL (proxy):", baseURL);
}

export const api = axios.create({
  baseURL,
  withCredentials: true // send auth cookie
});

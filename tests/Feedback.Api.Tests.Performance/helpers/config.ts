import { RefinedResponse, ResponseType } from "k6/http";

export const BASE_URL: string = __ENV.BASE_URL || "http://localhost:5067";

export const ENDPOINTS = {
  feedback: `${BASE_URL}/api/feedback`,
  feedbackById: (id: number) => `${BASE_URL}/api/feedback/${id}`,
  feedbackStatus: (id: number) => `${BASE_URL}/api/feedback/${id}/status`,
  feedbackVote: (id: number) => `${BASE_URL}/api/feedback/${id}/vote`,
  feedbackComments: (id: number) => `${BASE_URL}/api/feedback/${id}/comments`,
  stats: `${BASE_URL}/api/feedback/stats`,
} as const;

export const HEADERS: Record<string, string> = {
  "Content-Type": "application/json",
};

export const THRESHOLDS: Record<string, string[]> = {
  http_req_duration: ["p(95)<500", "p(99)<1000"],
  http_req_failed: ["rate<0.01"],
};

export interface CreateFeedbackRequest {
  title: string;
  description: string;
  type: number; // 0=Bug, 1=Feature, 2=Improvement
  priority: number; // 0=Low, 1=Medium, 2=High
  authorName: string;
  authorEmail: string;
}

export interface FeedbackResponse {
  id: number;
  title: string;
  status: string;
  type: string;
  voteCount: number;
}

export function parseBody<T>(res: RefinedResponse<ResponseType>): T {
  return JSON.parse(res.body as string) as T;
}

export function randomEmail(prefix: string): string {
  return `${prefix}-${__VU}-${__ITER}@perf-test.com`;
}

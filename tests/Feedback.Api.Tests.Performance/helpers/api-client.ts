import http, { RefinedResponse, ResponseType } from "k6/http";
import {
  ENDPOINTS,
  HEADERS,
  CreateFeedbackRequest,
} from "./config.ts";

export function getAllFeedback(sortByVotes = false): RefinedResponse<ResponseType> {
  const url = sortByVotes
    ? `${ENDPOINTS.feedback}?sortByVotes=true`
    : ENDPOINTS.feedback;
  return http.get(url, { headers: HEADERS });
}

export function getFeedbackById(id: number): RefinedResponse<ResponseType> {
  return http.get(ENDPOINTS.feedbackById(id), { headers: HEADERS });
}

export function createFeedback(payload: CreateFeedbackRequest): RefinedResponse<ResponseType> {
  return http.post(ENDPOINTS.feedback, JSON.stringify(payload), { headers: HEADERS });
}

export function updateStatus(id: number, status: number): RefinedResponse<ResponseType> {
  return http.patch(
    ENDPOINTS.feedbackStatus(id),
    JSON.stringify({ status }),
    { headers: HEADERS }
  );
}

export function addVote(id: number, voterEmail: string): RefinedResponse<ResponseType> {
  return http.post(
    ENDPOINTS.feedbackVote(id),
    JSON.stringify({ voterEmail }),
    { headers: HEADERS }
  );
}

export function removeVote(id: number, voterEmail: string): RefinedResponse<ResponseType> {
  return http.del(
    `${ENDPOINTS.feedbackVote(id)}?voterEmail=${encodeURIComponent(voterEmail)}`,
    undefined,
    { headers: HEADERS }
  );
}

export function addComment(
  id: number,
  authorName: string,
  content: string,
  isOfficial: boolean
): RefinedResponse<ResponseType> {
  return http.post(
    ENDPOINTS.feedbackComments(id),
    JSON.stringify({ authorName, content, isOfficial }),
    { headers: HEADERS }
  );
}

export function getStats(): RefinedResponse<ResponseType> {
  return http.get(ENDPOINTS.stats, { headers: HEADERS });
}

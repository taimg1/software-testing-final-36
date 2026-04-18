// Smoke test: 1 VU, 30s — verify API is alive and basic CRUD works
import { check, sleep } from "k6";
import { Options } from "k6/options";
import { THRESHOLDS, FeedbackResponse, parseBody, randomEmail } from "../helpers/config.ts";
import {
  getAllFeedback,
  createFeedback,
  getFeedbackById,
  addVote,
  getStats,
} from "../helpers/api-client.ts";

export const options: Options = {
  vus: 1,
  duration: "30s",
  thresholds: THRESHOLDS,
};

export default function () {
  // List all feedback (with sort)
  const listRes = getAllFeedback(true);
  check(listRes, { "GET /api/feedback?sortByVotes=true → 200": (r) => r.status === 200 });

  // Get stats
  const statsRes = getStats();
  check(statsRes, { "GET /api/feedback/stats → 200": (r) => r.status === 200 });

  // Create feedback
  const createRes = createFeedback({
    title: `Smoke test feedback ${__VU}-${__ITER}`,
    description: "Performance smoke test description",
    type: 1, // Feature
    priority: 1, // Medium
    authorName: "k6 Smoke",
    authorEmail: randomEmail("smoke-author"),
  });
  check(createRes, { "POST /api/feedback → 201": (r) => r.status === 201 });

  if (createRes.status === 201) {
    const feedback = parseBody<FeedbackResponse>(createRes);

    // Get by id
    const getRes = getFeedbackById(feedback.id);
    check(getRes, { "GET /api/feedback/{id} → 200": (r) => r.status === 200 });

    // Vote
    const voteRes = addVote(feedback.id, randomEmail("smoke-voter"));
    check(voteRes, { "POST /api/feedback/{id}/vote → 201": (r) => r.status === 201 });
  }

  sleep(1);
}

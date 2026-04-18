// Load test: Ramp to 20 VUs — test feedback list with sorting under sustained load
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
  stages: [
    { duration: "1m", target: 10 },
    { duration: "3m", target: 20 },
    { duration: "1m", target: 0 },
  ],
  thresholds: THRESHOLDS,
};

export default function () {
  // Heavily test the list endpoint with sorting (most common read path)
  const listSortedRes = getAllFeedback(true);
  check(listSortedRes, { "GET feedback sorted by votes → 200": (r) => r.status === 200 });

  const listRes = getAllFeedback(false);
  check(listRes, { "GET feedback by date → 200": (r) => r.status === 200 });

  // Stats
  const statsRes = getStats();
  check(statsRes, { "GET stats → 200": (r) => r.status === 200 });

  // Create + vote
  const authorEmail = randomEmail(`load-author-${__VU}`);
  const createRes = createFeedback({
    title: `Load test ${__VU}-${__ITER}`,
    description: "Load testing the feedback submission endpoint with concurrent users",
    type: __ITER % 3,
    priority: __ITER % 3,
    authorName: `VU ${__VU}`,
    authorEmail: authorEmail,
  });
  check(createRes, { "POST feedback → 201": (r) => r.status === 201 });

  if (createRes.status === 201) {
    const feedback = parseBody<FeedbackResponse>(createRes);

    const getRes = getFeedbackById(feedback.id);
    check(getRes, { "GET feedback by id → 200": (r) => r.status === 200 });

    const voteRes = addVote(feedback.id, randomEmail(`load-voter-${__VU}`));
    check(voteRes, { "POST vote → 201": (r) => r.status === 201 });
  }

  sleep(0.5);
}

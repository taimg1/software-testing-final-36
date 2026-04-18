// Stress test: Push to 100 VUs to find breaking point — focus on concurrent voting
import { check, sleep } from "k6";
import { Options } from "k6/options";
import { FeedbackResponse, parseBody, randomEmail } from "../helpers/config.ts";
import {
  getAllFeedback,
  createFeedback,
  addVote,
  removeVote,
} from "../helpers/api-client.ts";

export const options: Options = {
  stages: [
    { duration: "1m", target: 10 },
    { duration: "2m", target: 10 },
    { duration: "1m", target: 50 },
    { duration: "2m", target: 50 },
    { duration: "1m", target: 100 },
    { duration: "2m", target: 100 },
    { duration: "2m", target: 0 },
  ],
  thresholds: {
    http_req_duration: ["p(95)<1000", "p(99)<2000"],
    http_req_failed: ["rate<0.05"],
  },
};

export default function () {
  // Stress concurrent voting — the most write-heavy operation
  const authorEmail = randomEmail(`stress-author-${__VU}`);
  const createRes = createFeedback({
    title: `Stress test ${__VU}-${__ITER}`,
    description: "Stress testing concurrent vote operations",
    type: 0, // Bug
    priority: 2, // High
    authorName: `Stress VU ${__VU}`,
    authorEmail: authorEmail,
  });

  if (createRes.status === 201) {
    const feedback = parseBody<FeedbackResponse>(createRes);
    const voterEmail = randomEmail(`stress-voter-${__VU}`);

    // Add vote
    const voteRes = addVote(feedback.id, voterEmail);
    check(voteRes, {
      "POST vote (stress) → 201 or 409": (r) =>
        r.status === 201 || r.status === 409,
    });

    // Remove vote
    if (voteRes.status === 201) {
      const removeRes = removeVote(feedback.id, voterEmail);
      check(removeRes, {
        "DELETE vote (stress) → 204 or 404": (r) =>
          r.status === 204 || r.status === 404,
      });
    }
  }

  // Also load the list with sorting under stress
  const listRes = getAllFeedback(true);
  check(listRes, { "GET sorted list (stress) → 200": (r) => r.status === 200 });

  sleep(0.3);
}

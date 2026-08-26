import { analyzeCommits as analyzeConventionalCommits } from "@semantic-release/commit-analyzer";
import { generateNotes as generateConventionalNotes } from "@semantic-release/release-notes-generator";

function escapeRegularExpression(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function applicationContext(scope, context) {
  if (!scope) {
    throw new TypeError("The application semantic-release plugin requires a scope.");
  }

  const escapedScope = escapeRegularExpression(scope);
  const scopedHeader = new RegExp(`^[a-zA-Z]+\\(${escapedScope}\\)!?:`);
  return {
    ...context,
    commits: context.commits.filter(({ message }) => scopedHeader.test(message)),
  };
}

export function analyzeCommits({ scope, ...pluginConfig }, context) {
  return analyzeConventionalCommits(pluginConfig, applicationContext(scope, context));
}

export function generateNotes({ scope, ...pluginConfig }, context) {
  return generateConventionalNotes(pluginConfig, applicationContext(scope, context));
}

// Keep test credentials out of assertion/Playwright diagnostic logs.
const secrets = new Set([process.env.ITAM_TEST_PASSWORD, process.env.ITAM_ADMIN_TOKEN].filter(Boolean));
function rememberSecret(value) { if (value) secrets.add(value); return value; }
function redactError(error) {
  let message = String(error?.stack || error?.message || error);
  for (const value of secrets) message = message.split(value).join('[REDACTED]');
  return message
    .replace(/eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/g, '[TOKEN]')
    .replace(/[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}/gi, '[ID]');
}
module.exports = { rememberSecret, redactError };

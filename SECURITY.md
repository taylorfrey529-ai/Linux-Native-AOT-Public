# Security Policy

## Supported source

Security fixes are accepted against the latest public `main` branch. Historical snapshots and historical Native AOT artifacts are retained for reproducibility but are not separately supported.

## Reporting a vulnerability

Please do not disclose a suspected vulnerability in a public issue before maintainers have had an opportunity to investigate it.

Use GitHub's private **Report a vulnerability** feature when it is available for this repository. When that feature is not visible, contact the repository owner privately through their GitHub profile and include:

- the affected commit and component;
- a minimal reproduction;
- expected and observed behavior;
- security impact;
- relevant platform, RID, SDK, and Native AOT package versions;
- any proposed mitigation.

Do not include live credentials, personal data, or third-party confidential material in a report.

## Scope

Relevant reports include unsafe parsing or deserialization, command or path injection, archive traversal, package-integrity bypass, evidence-verification bypass, denial-of-service through bounded-input failures, CI permission problems, and Native AOT artifact-validation defects.

This repository is not a hosted production service. Authentication, networking, persistence, payments, moderation, anti-cheat, deployment, and live operations are outside the implemented product boundary. Reports about missing production features are not security vulnerabilities unless they demonstrate a defect in an implemented claim.

## Disclosure

Maintainers will acknowledge and triage reports through the private channel used for submission. Public disclosure should wait until a fix or documented mitigation is available, unless immediate disclosure is required to prevent ongoing harm.

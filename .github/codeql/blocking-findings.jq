# Reads a CodeQL SARIF file and emits one object per finding, with `blocking` set when
# the build must fail: security-severity >= 7.0 (High or Critical), or an error-level
# result whose rule metadata cannot be found (fail safe rather than silently pass).
# Accepted risk (still reported, not blocking), PTL.Api only: the API has no authentication of
# its own and no cookie session, and is reachable only from the PTL web apps inside the VPC, which
# authenticate and authorise users. The function-level access-control rule therefore flags every
# action, and the CSRF token rule does not apply because CSRF needs cookie-based authentication.
# Revisit if the API is ever exposed beyond the web apps.
# Results in tests/, vendored wwwroot/lib/ and minified files are ignored.
[ .runs[] as $run
  | ([($run.tool.driver.rules // [])[], (($run.tool.extensions // [])[] | (.rules // [])[])]) as $rules
  | ($run.results // [])[]
  | . as $r
  | (([$rules[] | select(.id == $r.ruleId)] | .[0]) // {}) as $rule
  | { rule: $r.ruleId,
      level: ($r.level // "warning"),
      known: ($rule | length > 0),
      severity: (($rule.properties["security-severity"] // "0") | tonumber),
      file: ($r.locations[0].physicalLocation.artifactLocation.uri // "unknown"),
      line: ($r.locations[0].physicalLocation.region.startLine // 0),
      message: ($r.message.text // "") }
  | select((.file | test("^(tests/|.*/wwwroot/lib/)|\\.min\\.js$")) | not)
  | (.rule | IN("cs/web/missing-function-level-access-control", "cs/web/missing-token-validation")) as $ruleAccepted
  | (.file | startswith("src/PTL.Api/")) as $inApi
  | . + { accepted: ($ruleAccepted and $inApi) }
  | . + { blocking: ((.accepted | not) and (.severity >= 7 or (.known == false and .level == "error"))) } ]

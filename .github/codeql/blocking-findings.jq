# Reads a CodeQL SARIF file and emits one object per finding, with `blocking` set when
# the build must fail: security-severity >= 7.0 (High or Critical), or an error-level
# result whose rule metadata cannot be found (fail safe rather than silently pass).
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
  | . + { blocking: (.severity >= 7 or (.known == false and .level == "error")) } ]

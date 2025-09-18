# GitHub Workflows

## add-to-project.yml

This workflow automatically manages new issues in the repository:

### Triggers
- Runs when new issues are opened

### Jobs

#### add-to-project
- Automatically adds new issues to the Ignite GitHub Project
- Requires `IGNITE_PROJECT_URL` repository variable to be configured
- Uses the standard GitHub token for authentication

#### label_issues  
- Automatically applies labels to new issues based on content
- Default labels applied to all issues: `triage`, `needs-review`
- Smart labeling based on issue title and body:
  - `bug` - for issues containing "bug" or "error"
  - `enhancement` - for feature requests
  - `documentation` - for docs-related issues
  - `question` - for help requests
  - `lab-related` - for lab/workshop issues
  - `wpf-ai` - for WPF and AI-related issues

### Configuration Required

Set the following repository variable:
- `IGNITE_PROJECT_URL` - URL of the GitHub Project where issues should be added

The workflow uses standard GitHub permissions and tokens, no additional secrets required.
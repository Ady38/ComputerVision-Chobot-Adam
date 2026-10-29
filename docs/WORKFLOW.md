# Create and use your own repository

This repository is a private template. Ask the instructor for access and
accept the GitHub invitation. Detailed Slovak instructions are available
in [NAVOD.md](sk/NAVOD.md).

## Create a copy on GitHub

1. Sign in and open the template supplied by the instructor.
2. Select **Use this template → Create a new repository**.
3. Choose your own account as owner and a repository name without spaces.
4. Select **Private** and leave **Include all branches** unchecked.
5. Create the repository.

GitHub creates independent history starting with one commit. Later changes
to the template are not automatically copied into your repository.
See [GitHub's template instructions](https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-repository-from-a-template).

Clone **your** repository. Replace the placeholders with its actual address:

```sh
git clone https://github.com/YOUR-ACCOUNT/YOUR-REPOSITORY.git
cd YOUR-REPOSITORY
git remote -v
git switch -c solution
```

`origin` should point to your repository. Keep `main` as the starting version
and work on `solution`. Build and run the tests using the [README](../README.md).

## Save changes

Review and stage only the files you intend to save. For example:

```sh
git status
git diff
git add ComputerVision tests
git diff --cached
git commit -m "Describe the change"
git push -u origin solution
```

Include other changed paths explicitly when appropriate. `bin/`, `obj/`,
IDE state and `results/` are ignored. A commit records local changes;
`push` sends committed changes to GitHub.

## Give the instructor access and open a review

In your repository, use **Settings → Collaborators → Add people** to invite
`mikelekyr`, unless the instructor supplies a different account. Access
becomes available after the invitation is accepted.
[GitHub's invitation instructions](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/repository-access-and-collaboration/inviting-collaborators-to-a-personal-repository).

Open a pull request **inside your own repository**, with base `main` and
compare `solution`. Describe what changed and how to run it. Share its URL
with the instructor and keep it open while review is in progress.
The template is not the target of this PR: repositories created from it
have independent histories.

## Access to the shared template

Private repositories on a personal account give collaborators write access;
they cannot offer a read-only collaborator role. Organization repositories
can grant **Read** to individual students or a team. The instructor chooses
and arranges access before distributing the template.
[Personal-account permissions](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/repository-access-and-collaboration/permission-levels-for-a-personal-account-repository),
[organization roles](https://docs.github.com/en/organizations/managing-user-access-to-your-organizations-repositories/managing-repository-roles/repository-roles-for-an-organization).

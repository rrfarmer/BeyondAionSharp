#!/bin/sh
set -eu

for repository in /workspace/csharp /workspace/java; do
    if [ ! -d "$repository/.git" ]; then
        echo "Git repository is not mounted at $repository" >&2
        exit 1
    fi
    if ! git config --global --get-all safe.directory | grep -Fqx "$repository"; then
        git config --global --add safe.directory "$repository"
    fi
done

git config --global user.name "${GIT_COMMITTER_NAME:-BeyondAionSharp Codex}"
git config --global user.email "${GIT_COMMITTER_EMAIL:-codex@local.invalid}"
# Both bind-mounted repositories were checked out by Git for Windows with
# core.autocrlf=true. Match that normalization so Linux Git sees the same tree.
git config --global core.autocrlf true
git config --global init.defaultBranch main

exec "$@"

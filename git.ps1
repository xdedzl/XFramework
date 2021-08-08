git branch

git branch -D 1.0
git push origin --delete 1.0

git tag -d 1.0.0
git push origin :refs/tags/1.0.0

git subtree split --prefix=XFramework/Assets/XFramework/Core --branch 1.0
git tag 1.0.0 1.0
git push origin 1.0 --tags
pause
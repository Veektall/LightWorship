# Publish To GitHub

Use this only after reviewing the files and confirming you want LightWorship to be public under the MIT license.

## 1. Create The Repo

Open:

https://github.com/new

Recommended settings:

- Repository name: `LightWorship`
- Visibility: Public
- Do not add README, license, or gitignore on GitHub because they already exist locally.

## 2. Configure Git Identity

In PowerShell inside this folder:

```powershell
git config user.name "Your Name"
git config user.email "you@example.com"
```

Use the email you want attached to the commit. GitHub noreply email is fine.

## 3. Prepare First Commit

```powershell
powershell -ExecutionPolicy Bypass -File .\Prepare-PublicRepo.ps1
```

The script checks publishable files, scans for obvious API keys, and creates the first commit.

## 4. Push

Replace `YOUR_USERNAME`:

```powershell
git remote add origin https://github.com/YOUR_USERNAME/LightWorship.git
git branch -M main
git push -u origin main
```

## 5. Submit OpenAI Form

Use:

https://openai.com/form/codex-for-oss/

Prepared answers are in:

`OPENAI_OSS_APPLICATION_PACKET.md`

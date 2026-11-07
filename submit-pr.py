"""
Tự động hóa ủy quyền GitHub qua Device Code và tạo Pull Request tự động.
Hoạt động độc lập 100% bằng thư viện chuẩn Python (không cần cài thêm package).
Sử dụng GitHub App Client ID (tương thích OAuth Device Code Flow).
"""
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import webbrowser
from pathlib import Path

# GitHub App Client ID (hỗ trợ Device Flow)
CLIENT_ID = "Ov23li4g1C3hqWizG52D"
SCOPES = "repo workflow user:email"


def get_token_file() -> Path:
    """Đường dẫn lưu token tại thư mục user để máy nào cũng tự lưu riêng."""
    base = Path(os.environ.get("USERPROFILE") or Path.home()) / ".internship-portal"
    base.mkdir(parents=True, exist_ok=True)
    return base / "credentials.json"


def save_token(token: str, login: str, email: str | None = None):
    try:
        data = {
            "token": token,
            "login": login,
            "email": email,
            "saved_at": time.strftime("%Y-%m-%d %H:%M:%S")
        }
        get_token_file().write_text(json.dumps(data, indent=2), encoding="utf-8")
    except Exception as e:
        print(f"[WARN] Không thể lưu token: {e}")


def load_token() -> str | None:
    # 1. Biến môi trường
    for env in ("GH_TOKEN", "GITHUB_TOKEN"):
        val = os.environ.get(env)
        if val:
            return val.strip()

    # 2. File credentials trong thư mục người dùng
    p = get_token_file()
    if p.is_file():
        try:
            data = json.loads(p.read_text(encoding="utf-8"))
            return data.get("token")
        except Exception:
            pass
    return None


def clear_token():
    try:
        p = get_token_file()
        if p.is_file():
            p.unlink()
    except Exception:
        pass


def http_json(url: str, method: str = "GET", data: dict | None = None, headers: dict | None = None) -> tuple[int, dict]:
    """Gọi HTTP request trả về (status_code, json_dict) dùng thư viện chuẩn urllib."""
    req_headers = {
        "Accept": "application/vnd.github+json",
        "User-Agent": "InternshipPortal-Tool/1.0",
        "X-GitHub-Api-Version": "2022-11-28",
    }
    if headers:
        req_headers.update(headers)

    encoded_data = None
    if data is not None:
        if req_headers.get("Content-Type") == "application/x-www-form-urlencoded":
            encoded_data = urllib.parse.urlencode(data).encode("utf-8")
        else:
            req_headers["Content-Type"] = "application/json"
            encoded_data = json.dumps(data).encode("utf-8")

    req = urllib.request.Request(url, data=encoded_data, headers=req_headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            content = resp.read().decode("utf-8")
            return resp.status, json.loads(content) if content else {}
    except urllib.error.HTTPError as e:
        content = e.read().decode("utf-8")
        try:
            body = json.loads(content)
        except Exception:
            body = {"message": content}
        return e.code, body


def copy_to_clipboard(text: str):
    try:
        p = subprocess.Popen(["clip"], stdin=subprocess.PIPE, shell=True)
        p.communicate(text.encode("utf-8"))
    except Exception:
        pass


def get_authenticated_user(token: str) -> dict | None:
    status, res = http_json("https://api.github.com/user", headers={"Authorization": f"Bearer {token}"})
    if status == 200 and "login" in res:
        return res
    return None


def run_device_flow() -> str:
    """Kích hoạt Device Code Flow để người dùng cấp quyền."""
    print("\n" + "=" * 62)
    print("  ỦY QUYỀN GITHUB BẰNG DEVICE CODE")
    print("=" * 62)

    status, code_res = http_json(
        "https://github.com/login/device/code",
        method="POST",
        data={"client_id": CLIENT_ID, "scope": SCOPES},
        headers={"Content-Type": "application/x-www-form-urlencoded", "Accept": "application/json"}
    )

    if status != 200 or "device_code" not in code_res:
        raise RuntimeError(f"Không thể khởi tạo Device Flow: {code_res}")

    user_code = code_res["user_code"]
    device_code = code_res["device_code"]
    uri = code_res.get("verification_uri", "https://github.com/login/device")
    interval = int(code_res.get("interval", 5))
    expires_in = int(code_res.get("expires_in", 900))

    copy_to_clipboard(user_code)

    print(f"\n  1. Mở trang xác nhận:  \033[96m{uri}\033[0m")
    print(f"  2. Nhập mã ủy quyền:   \033[93m\033[1m{user_code}\033[0m (Đã copy sẵn vào Clipboard!)")
    print("\n  Trình duyệt đang tự động mở trang web...")
    print("  Hệ thống đang chờ bạn bấm [Authorize] trên GitHub...\n")

    try:
        webbrowser.open(uri)
    except Exception:
        pass

    deadline = time.monotonic() + expires_in
    while time.monotonic() < deadline:
        time.sleep(interval)
        status, poll = http_json(
            "https://github.com/login/oauth/access_token",
            method="POST",
            data={
                "client_id": CLIENT_ID,
                "device_code": device_code,
                "grant_type": "urn:ietf:params:oauth:grant-type:device_code"
            },
            headers={"Content-Type": "application/x-www-form-urlencoded", "Accept": "application/json"}
        )

        if "access_token" in poll:
            return poll["access_token"]

        err = poll.get("error")
        if err == "authorization_pending":
            continue
        if err == "slow_down":
            interval = int(poll.get("interval", interval + 5))
            continue
        raise RuntimeError(f"Lỗi xác thực: {poll.get('error_description') or err}")

    raise TimeoutError("Mã Device Code đã hết hạn trước khi bạn hoàn tất xác nhận.")


def get_token() -> tuple[str, dict]:
    token = load_token()
    if token:
        user = get_authenticated_user(token)
        if user:
            print(f"[AUTH] Đã nhận diện tài khoản GitHub: {user.get('login')}")
            return token, user
        print("[AUTH] Token lưu trên máy đã hết hạn, tiến hành cấp quyền mới...")
        clear_token()

    token = run_device_flow()
    user = get_authenticated_user(token) or {"login": "unknown"}
    save_token(token, user.get("login", ""), user.get("email"))
    print(f"[OK] Ủy quyền thành công cho tài khoản: {user.get('login')}")
    return token, user


def get_repo_slug() -> tuple[str, str, str]:
    res = subprocess.run(["git", "remote", "get-url", "origin"], capture_output=True, text=True)
    url = res.stdout.strip()

    while not url:
        print("\n[CHÚ Ý] Chưa cấu hình Git Remote 'origin' cho kho này.")
        entered = input("Nhập URL GitHub repository (vd: https://github.com/Username/InternshipPortal): ").strip()
        if entered:
            subprocess.run(["git", "remote", "add", "origin", entered], check=True)
            url = entered
            break

    m = re.search(r"github\.com[:/]([^/]+)/([^/\.]+)(?:\.git)?", url)
    if not m:
        raise ValueError(f"Không nhận diện được định dạng GitHub từ URL: {url}")
    return url, m.group(1), m.group(2)


def main():
    print("===================================================")
    print(" [1/3] Kiem tra uy quyen GitHub...")
    print("===================================================")
    token, user = get_token()

    print("\n===================================================")
    print(" [2/3] Day ma nguon len GitHub...")
    print("===================================================")
    remote_url, owner, repo = get_repo_slug()
    branch = "feature/day-1-views"

    auth_push_url = f"https://x-access-token:{token}@github.com/{owner}/{repo}.git"
    print(f"Dang day nhanh {branch} len {owner}/{repo}...")
    push_res = subprocess.run(
        ["git", "push", auth_push_url, f"{branch}:{branch}", "--force"],
        capture_output=True, text=True
    )

    if push_res.returncode != 0:
        print(f"[ERROR] Loi khi day code: {push_res.stderr}")
        sys.exit(1)
    print(f"[OK] Da push thanh cong nhanh {branch}!")

    print("\n===================================================")
    print(" [3/3] Tu dong tao Pull Request vao main...")
    print("===================================================")

    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github+json"
    }

    # Lấy base branch mặc định
    _, repo_info = http_json(f"https://api.github.com/repos/{owner}/{repo}", headers=headers)
    base_branch = repo_info.get("default_branch", "main")

    # Kiểm tra xem PR đã mở chưa
    status, pulls = http_json(
        f"https://api.github.com/repos/{owner}/{repo}/pulls?head={owner}:{branch}&base={base_branch}&state=open",
        headers=headers
    )

    if isinstance(pulls, list) and len(pulls) > 0:
        print(f"\n[OK] Pull Request da co san tren GitHub:")
        print(f"     => {pulls[0]['html_url']}")
    else:
        pr_body = (
            "## 📌 Hoàn thành tầng Data, Seeding & Giao diện Ngày 1\n\n"
            "- Models: Company, JobPost, StudentProfile, Application\n"
            "- ApplicationDbContext & DbInitializer (Seeding 2 Company, 3 JobPost)\n"
            "- Cấu hình LocalDB trong appsettings.json và Program.cs\n"
            "- JobController (Index, Detail) & Giao diện Views Bootstrap 5"
        )

        create_status, create_res = http_json(
            f"https://api.github.com/repos/{owner}/{repo}/pulls",
            method="POST",
            data={
                "title": "feat(views): Hoàn thiện giao diện Ngày 1",
                "head": branch,
                "base": base_branch,
                "body": pr_body
            },
            headers=headers
        )

        if create_status == 201:
            print(f"\n===================================================")
            print(f" [THÀNH CÔNG] ĐÃ TỰ ĐỘNG TẠO PULL REQUEST!")
            print(f" URL: {create_res.get('html_url')}")
            print(f"===================================================")
        else:
            compare_url = f"https://github.com/{owner}/{repo}/compare/{base_branch}...{branch}?expand=1"
            print(f"[NOTE] Link tao PR tren trinh duyet: {compare_url}")


if __name__ == "__main__":
    main()

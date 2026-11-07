"""
Tự động hóa ủy quyền GitHub qua Device Code và tạo Pull Request tự động.
Sử dụng GitHub App từ C:\\Users\\admin\\Desktop\\TXA-PACKAGE.
"""
import os
import re
import subprocess
import sys
import time
import webbrowser

# Thêm đường dẫn TXA-PACKAGE để sử dụng txa.core
TXA_PACKAGE_DIR = r"C:\Users\admin\Desktop\TXA-PACKAGE"
TXA_SRC_DIR = os.path.join(TXA_PACKAGE_DIR, "tools", "src")
if os.path.isdir(TXA_SRC_DIR) and TXA_SRC_DIR not in sys.path:
    sys.path.insert(0, TXA_SRC_DIR)

try:
    from txa.core import credentials
    from txa.core.github import GitHub, GitHubError, device_flow
    from txa.constants import GITHUB_CLIENT_ID
except ImportError:
    # Dự phòng nếu không import được từ txa package
    GITHUB_CLIENT_ID = "Ov23li4g1C3hqWizG52D"
    credentials = None
    GitHub = None
    GitHubError = Exception
    device_flow = None


CLIENT_ID = GITHUB_CLIENT_ID or "Ov23li4g1C3hqWizG52D"
SCOPES = "repo workflow user:email"


def run_cmd(args, check=True) -> subprocess.CompletedProcess:
    return subprocess.run(args, capture_output=True, text=True, check=check, encoding="utf-8")


def copy_to_clipboard(text: str):
    try:
        p = subprocess.Popen(["clip"], stdin=subprocess.PIPE, shell=True)
        p.communicate(text.encode("utf-8"))
    except Exception:
        pass


def get_authenticated_client() -> tuple[any, str]:
    """Lấy GitHub client và token. Nếu chưa có hoặc hết hạn thì chạy Device Flow."""
    token = None
    if credentials:
        token, _ = credentials.load_token()

    if not token:
        token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")

    if token and GitHub:
        try:
            gh = GitHub(token)
            user = gh.get("user")
            print(f"[AUTH] Đã xác thực với tài khoản GitHub: {user.get('login')}")
            return gh, token
        except Exception:
            print("[AUTH] Token lưu trữ đã hết hạn. Đang thực hiện ủy quyền mới qua Device Code...")
            if credentials:
                credentials.clear_token()
            token = None

    # Thực hiện Device Flow
    print("\n" + "=" * 62)
    print("  ỦY QUYỀN GITHUB BẰNG DEVICE CODE (GITHUB APP TỪ TXA-PACKAGE)")
    print("=" * 62)

    def on_code(code: dict):
        user_code = code["user_code"]
        uri = code.get("verification_uri", "https://github.com/login/device")
        copy_to_clipboard(user_code)

        print(f"\n  1. Mở trang:   \033[96m{uri}\033[0m")
        print(f"  2. Nhập mã:    \033[93m\033[1m{user_code}\033[0m (Đã sao chép vào Clipboard)")
        print("\n  Trình duyệt đang tự động mở trang nhập mã...")
        print("  Đang chờ bạn bấm [Authorize] trên GitHub...\n")
        try:
            webbrowser.open(uri)
        except Exception:
            pass

    if device_flow and GitHub:
        token = device_flow(CLIENT_ID, SCOPES, on_code)
        gh = GitHub(token)
        user = gh.get("user")
        if credentials:
            credentials.save_token(token, user["login"], "device flow", user.get("email"))
        print(f"\n[OK] Ủy quyền thành công! Đăng nhập: {user.get('login')}")
        return gh, token
    else:
        # Fallback tự gửi request nếu không có txa library
        import requests
        r = requests.post("https://github.com/login/device/code",
                          data={"client_id": CLIENT_ID, "scope": SCOPES},
                          headers={"Accept": "application/json"}, timeout=30)
        data = r.json()
        on_code(data)
        device_code = data["device_code"]
        interval = int(data.get("interval", 5))
        deadline = time.monotonic() + int(data.get("expires_in", 900))

        while time.monotonic() < deadline:
            time.sleep(interval)
            poll = requests.post("https://github.com/login/oauth/access_token",
                                 data={
                                     "client_id": CLIENT_ID,
                                     "device_code": device_code,
                                     "grant_type": "urn:ietf:params:oauth:grant-type:device_code"
                                 },
                                 headers={"Accept": "application/json"}, timeout=30).json()
            if "access_token" in poll:
                token = poll["access_token"]
                break
            err = poll.get("error")
            if err == "authorization_pending":
                continue
            if err == "slow_down":
                interval = int(poll.get("interval", interval + 5))
                continue
            raise RuntimeError(f"Device code error: {err}")

        gh = GitHub(token) if GitHub else None
        return gh, token


def get_repo_slug() -> tuple[str, str, str]:
    """Lấy remote origin và bóc tách owner/repo."""
    res = subprocess.run(["git", "remote", "get-url", "origin"], capture_output=True, text=True)
    url = res.stdout.strip()

    while not url:
        print("\n[CHÚ Ý] Chưa cấu hình Git Remote 'origin' cho kho lưu trữ này.")
        entered = input("Vui lòng nhập URL GitHub repository (vd: https://github.com/Username/InternshipPortal): ").strip()
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
    gh, token = get_authenticated_client()

    print("\n===================================================")
    print(" [2/3] Day ma nguon len GitHub qua Access Token...")
    print("===================================================")
    remote_url, owner, repo = get_repo_slug()
    branch = "feature/day-1-views"

    # Push bằng token URL để không cần hỏi pass/SSH key
    auth_push_url = f"https://x-access-token:{token}@github.com/{owner}/{repo}.git"
    print(f"Dang push nhanh {branch} len {owner}/{repo}...")
    push_res = subprocess.run(["git", "push", auth_push_url, f"{branch}:{branch}", "--force"],
                              capture_output=True, text=True)
    if push_res.returncode != 0:
        print(f"[ERROR] Loi khi push: {push_res.stderr}")
        sys.exit(1)
    print(f"[OK] Da push thanh cong nhanh {branch}!")

    print("\n===================================================")
    print(" [3/3] Tu dong tao Pull Request vao nhanh main...")
    print("===================================================")

    import requests
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28"
    }

    # Lấy base branch mặc định của repo
    repo_info = requests.get(f"https://api.github.com/repos/{owner}/{repo}", headers=headers).json()
    base_branch = repo_info.get("default_branch", "main")

    # Kiểm tra xem PR đã tồn tại chưa
    pulls_check = requests.get(
        f"https://api.github.com/repos/{owner}/{repo}/pulls",
        headers=headers,
        params={"head": f"{owner}:{branch}", "base": base_branch, "state": "open"}
    ).json()

    if isinstance(pulls_check, list) and len(pulls_check) > 0:
        pr_url = pulls_check[0]["html_url"]
        print(f"\n[OK] Pull Request da ton tai san tren GitHub:")
        print(f"     => {pr_url}")
    else:
        pr_body = (
            "## 📌 Hoàn thành tầng Data, Seeding & Giao diện Ngày 1\n\n"
            "### 1. Models\n"
            "- `Company`: Quản lý thông tin doanh nghiệp, trạng thái phê duyệt\n"
            "- `JobPost`: Tin tuyển dụng thực tập, liên kết Company\n"
            "- `StudentProfile`: Hồ sơ sinh viên, thông tin liên lạc\n"
            "- `Application`: Đơn ứng tuyển với enum `ApplicationStatus`\n\n"
            "### 2. Database & Seeding\n"
            "- `ApplicationDbContext` kế thừa `DbContext` với 4 DbSets\n"
            "- `DbInitializer` tự động seed 2 công ty (`FPT Software`, `Viettel Solutions`) và 3 tin tuyển dụng mẫu\n"
            "- Cấu hình LocalDB trong `appsettings.json`\n\n"
            "### 3. Controller & Views\n"
            "- `JobController`: Action `Index` và `Detail` kèm `Include(j => j.Company)`\n"
            "- Giao diện Bootstrap 5 cho trang danh sách và trang chi tiết việc làm\n"
            "- Đặt route mặc định về `Job/Index`"
        )

        create_res = requests.post(
            f"https://api.github.com/repos/{owner}/{repo}/pulls",
            headers=headers,
            json={
                "title": "feat(views): Hoàn thiện giao diện Ngày 1",
                "head": branch,
                "base": base_branch,
                "body": pr_body
            }
        )

        if create_res.status_code == 201:
            pr_data = create_res.json()
            pr_url = pr_data["html_url"]
            print(f"\n===================================================")
            print(f" [THÀNH CÔNG] ĐÃ TỰ ĐỘNG TẠO PULL REQUEST!")
            print(f" URL: {pr_url}")
            print(f"===================================================")
        else:
            print(f"[NOTE] Không thể tự tạo PR ({create_res.status_code}): {create_res.text}")
            compare_url = f"https://github.com/{owner}/{repo}/compare/{base_branch}...{branch}?expand=1"
            print(f"Ban co the mo link sau de tao PR thu cong: {compare_url}")


if __name__ == "__main__":
    main()


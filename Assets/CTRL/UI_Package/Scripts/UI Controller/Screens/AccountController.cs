using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Backend.Bootstrap;

namespace Game.UI
{
    /// <summary>
    /// Dieu khien tab TAI KHOAN: hien profile that + wire cac nut
    /// (doi ten, doi email, lien ket Google, dang xuat, xoa tai khoan, doi avatar).
    /// Dung ModalDialog cho nhap lieu / xac nhan / thong bao. Goi backend qua BackendBootstrap.
    /// </summary>
    public class AccountController : MonoBehaviour
    {
        [SerializeField] private Transform _root;
        [SerializeField] private string _loginScene = "Login_Register";

        private TMP_Text _nameTop, _idLabel, _nameValue, _emailValue;
        private Image _avatarImage;
        private string _currentAvatarUrl = "";
        // [FIX BUG-ACCOUNT 2026-09-14 - Aemy] nut LIEN KET Google
        private Image _googleLinkBg;
        private TMP_Text _googleLinkLabel;
        private Color _googleLinkDefaultColor;
        private bool _googleLinkColorCached;
        private static readonly Color GoogleLinkedGreen = new Color(0.30f, 0.84f, 0.53f, 1f);

        private void Awake()
        {
            if (_root == null) _root = transform;
            WireButton("THONG TIN CU THE/Ten hien thi/BT_DOI DISPLAYNAME", OnChangeName);
            WireButton("THONG TIN CU THE/Email/BT_DOI EMAIL", OnChangeEmail);
            WireButton("LIEN KET/BG/LIEN KET +", OnLinkGoogle);
            WireButton("bt_logout", OnLogout);
            WireButton("bt_delete", OnDeleteAccount);
            WireButton("Thong tin co ban/bt_change avt", OnChangeAvatar);

            _nameTop    = FindText("Thong tin co ban/displayname");
            _idLabel    = FindText("Thong tin co ban/ID");
            _nameValue  = FindText("THONG TIN CU THE/Ten hien thi/Operator_01");
            _emailValue = FindText("THONG TIN CU THE/Email/dia chi email");
            var av = Find("Thong tin co ban/Avatar"); if (av != null) _avatarImage = av.GetComponentInChildren<Image>();
            // [FIX BUG-ACCOUNT 2026-09-14 - Aemy] ref nut LIEN KET Google
            var bg = Find("LIEN KET/BG"); if (bg != null) _googleLinkBg = bg.GetComponent<Image>();
            _googleLinkLabel = FindText("LIEN KET/BG/LIEN KET +");
            if (_googleLinkBg != null) { _googleLinkDefaultColor = _googleLinkBg.color; _googleLinkColorCached = true; }
        }

        private void OnEnable() { LoadProfile(); }

        private void LoadProfile()
        {
            var svc = BackendBootstrap.PlayerProfileService;
            if (svc == null) return;
            LoadLinkedAccountInfo();   // [FIX BUG-ACCOUNT 2026-09-14 - Aemy]
            svc.GetMyProfile(p =>
            {
                if (p == null) return;
                ProfileCache.Set(p);
                if (_nameTop != null)   _nameTop.text   = p.DisplayName;
                if (_nameValue != null) _nameValue.text = p.DisplayName;
                if (_idLabel != null)   _idLabel.text   = "ID: " + p.PlayFabId;
                _currentAvatarUrl = p.AvatarUrl ?? "";
                if (!string.IsNullOrEmpty(p.AvatarUrl)) StartCoroutine(LoadAvatar(p.AvatarUrl));
            }, err => { });
        }

        // [FIX BUG-ACCOUNT 2026-09-14 - Aemy]
        private void LoadLinkedAccountInfo()
        {
            var ext = BackendBootstrap.ExternalAuthService;
            if (ext == null) return;
            ext.GetLinkedAccountInfo(info =>
            {
                if (info == null) return;
                if (_emailValue != null && !string.IsNullOrEmpty(info.Email)) _emailValue.text = info.Email;
                SetGoogleLinkedVisual(info.GoogleLinked);
            }, e => { Debug.LogWarning("[AccountController] Khong lay duoc thong tin tai khoan: " + (e != null ? e.ErrorMessage : "?")); });
        }

        private void SetGoogleLinkedVisual(bool linked)
        {
            if (_googleLinkBg != null)
                _googleLinkBg.color = linked ? GoogleLinkedGreen : (_googleLinkColorCached ? _googleLinkDefaultColor : _googleLinkBg.color);
            if (_googleLinkLabel != null)
                _googleLinkLabel.text = linked ? "ĐÃ LIÊN KẾT" : "LIÊN KẾT +";
        }

        private void OnChangeName()
        {
            if (ModalDialog.Instance == null) return;
            string cur = _nameValue != null ? _nameValue.text : "";
            ModalDialog.Instance.Prompt("ĐỔI TÊN HIỂN THỊ", cur, name =>
            {
                name = (name ?? "").Trim();
                if (name.Length < 3) { Info("Tên không hợp lệ", "Tên hiển thị cần ít nhất 3 ký tự."); return; }
                var svc = BackendBootstrap.PlayerProfileService;
                if (svc == null) { Info("Lỗi", "Backend chưa sẵn sàng."); return; }
                svc.SetDisplayName(name,
                    () => { ProfileCache.Invalidate(); LoadProfile(); Info("Thành công", "Đã đổi tên hiển thị thành: " + name); },
                    e => Info("Đổi tên thất bại", e != null ? e.ErrorMessage : "Lỗi không rõ."));
            }, "Nhập tên hiển thị mới (3-25 ký tự)", "Tên mới...");
        }

        private void OnChangeEmail()
        {
            if (ModalDialog.Instance == null) return;
            ModalDialog.Instance.Prompt("ĐỔI EMAIL LIÊN HỆ", "", email =>
            {
                email = (email ?? "").Trim();
                if (!email.Contains("@") || !email.Contains(".")) { Info("Email không hợp lệ", "Vui lòng nhập đúng định dạng email."); return; }
                var svc = BackendBootstrap.AccountRecoveryService;
                if (svc == null) { Info("Lỗi", "Backend chưa sẵn sàng."); return; }
                svc.SendEmailVerification(email,
                    () => { if (_emailValue != null) _emailValue.text = email; Info("Đã gửi xác thực", "Email liên hệ đã cập nhật. Kiểm tra hộp thư để xác thực."); },
                    e => Info("Đổi email thất bại", e != null ? e.ErrorMessage : "Lỗi không rõ."));
            }, "Nhập email liên hệ mới (sẽ gửi mail xác thực)", "email@example.com");
        }

        private async void OnLinkGoogle()
        {
            var google = BackendBootstrap.PCGoogleAuthService;
            var ext = BackendBootstrap.ExternalAuthService;
            if (google == null || ext == null) { Info("Lỗi", "Backend chưa sẵn sàng."); return; }
            if (ModalDialog.Instance != null) ModalDialog.Instance.Info("Liên kết Google", "Đang mở trình duyệt để đăng nhập Google...");
            string code = await google.GetServerAuthCodeAsync(BackendBootstrap.GoogleWebClientId, BackendBootstrap.GoogleLocalPort);
            if (string.IsNullOrEmpty(code)) { Info("Đã hủy", "Không lấy được mã Google (hủy hoặc lỗi)."); return; }
            ext.LinkGoogleAccount(code,
                () => { SetGoogleLinkedVisual(true); LoadLinkedAccountInfo(); Info("Thành công", "Đã liên kết tài khoản Google!"); },
                e => Info("Liên kết thất bại", e != null ? e.ErrorMessage : "Lỗi không rõ."));
        }

        private void OnLogout()
        {
            if (ModalDialog.Instance == null) return;
            ModalDialog.Instance.Confirm("ĐĂNG XUẤT", "Bạn có chắc muốn đăng xuất khỏi tài khoản?", () =>
            {
                var auth = BackendBootstrap.AuthService;
                if (auth != null) auth.Logout();
                SceneLoader.LoadWithLoading(_loginScene);
            }, "ĐĂNG XUẤT", "HỦY");
        }

        private void OnDeleteAccount()
        {
            if (ModalDialog.Instance == null) return;
            ModalDialog.Instance.Confirm("XÓA TÀI KHOẢN", "Hành động này KHÔNG THỂ hoàn tác. Toàn bộ dữ liệu sẽ bị xóa vĩnh viễn. Tiếp tục?", () =>
            {
                ModalDialog.Instance.Confirm("XÁC NHẬN LẦN CUỐI", "Bạn thật sự chắc chắn muốn XÓA tài khoản?", () =>
                {
                    var svc = BackendBootstrap.PlayerProfileService;
                    if (svc == null) { Info("Lỗi", "Backend chưa sẵn sàng."); return; }
                    Info("Đang xóa…", "Đang xóa tài khoản, vui lòng chờ.");
                    svc.DeleteMyAccount(
                        () =>
                        {
                            var auth = BackendBootstrap.AuthService;
                            if (auth != null) auth.Logout();
                            Info("Đã xóa tài khoản", "Tài khoản của bạn đã được xóa. Tạm biệt!");
                            SceneLoader.LoadWithLoading(_loginScene);
                        },
                        e => Info("Xóa thất bại", e != null ? e.ErrorMessage : "Lỗi không rõ."));
                }, "XÓA VĨNH VIỄN", "HỦY");
            }, "TIẾP TỤC", "HỦY");
        }

        private void OnChangeAvatar()
        {
            // [AVATAR 2026-09-27 - Aemy] Mo bang chon preset thay vi dan URL thu cong.
            AvatarPickerController.Open(url =>
            {
                if (string.IsNullOrEmpty(url)) return;
                ProfileCache.Invalidate();
                _currentAvatarUrl = url;
                StartCoroutine(LoadAvatar(url));
            });
        }

        private System.Collections.IEnumerator LoadAvatar(string url)
        {
            if (string.IsNullOrEmpty(url) || _avatarImage == null) yield break;
            using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req); if (tex != null) tex = Game.UI.ImageScale.Fit(tex, 256); // [#5] thu nho 64px
                    if (tex != null) { _avatarImage.color = Color.white; _avatarImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)); } // [AVATAR-FIX 2026-09-28 Aemy] reset mau trang
                }
            }
        }

        private void Info(string title, string msg) { if (ModalDialog.Instance != null) ModalDialog.Instance.Info(title, msg); }

        private void WireButton(string path, Action cb)
        {
            var t = Find(path); if (t == null) { Debug.LogWarning("[AccountController] Khong thay nut: " + path); return; }
            var btn = t.GetComponent<Button>(); if (btn == null) { Debug.LogWarning("[AccountController] Khong co Button: " + path); return; }
            btn.onClick.AddListener(() => { if (cb != null) cb(); });
        }
        private TMP_Text FindText(string path) { var t = Find(path); return t != null ? t.GetComponent<TMP_Text>() : null; }

        private Transform Find(string path)
        {
            Transform cur = _root;
            foreach (var part in path.Split('/'))
            {
                Transform next = null;
                foreach (Transform c in cur) { if (Norm(c.name) == Norm(part)) { next = c; break; } }
                if (next == null) return null;
                cur = next;
            }
            return cur;
        }
        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.ToLowerInvariant().Trim();
            var b = new System.Text.StringBuilder();
            foreach (char c in s) b.Append(Strip(c));
            return b.ToString();
        }
        private static char Strip(char c)
        {
            if ("áàảãạăắằẳẵặâấầẩẫậ".IndexOf(c) >= 0) return 'a';
            if ("éèẻẽẹêếềểễệ".IndexOf(c) >= 0) return 'e';
            if ("íìỉĩị".IndexOf(c) >= 0) return 'i';
            if ("óòỏõọôốồổỗộơớờởỡợ".IndexOf(c) >= 0) return 'o';
            if ("úùủũụưứừửữự".IndexOf(c) >= 0) return 'u';
            if ("ýỳỷỹỵ".IndexOf(c) >= 0) return 'y';
            if (c == 'đ') return 'd';
            return c;
        }
    }
}

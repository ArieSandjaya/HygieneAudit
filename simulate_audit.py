#!/usr/bin/env python3
"""
HygieneAudit — Audit Process Simulation
========================================
Menjalankan simulasi proses audit lengkap:
  1. Login ke aplikasi
  2. Ambil/buat Tenant
  3. Buat Audit baru
  4. Setiap item checklist: set status PASS/FAIL + upload foto
  5. Submit audit
  6. Tampilkan ringkasan hasil

Kebutuhan:
    pip install requests Pillow

Penggunaan:
    python simulate_audit.py --url http://localhost:44300 --username admin --password Admin123!
"""

import argparse
import io
import json
import os
import re
import sys
import time
from datetime import date
from typing import Optional

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    print("ERROR: Pillow belum terinstall. Jalankan: pip install Pillow")
    sys.exit(1)

try:
    import requests
except ImportError:
    print("ERROR: requests belum terinstall. Jalankan: pip install requests")
    sys.exit(1)


# ── Konfigurasi warna untuk foto tiap status ──────────────────────────────────

COLORS = {
    "PASS": [
        (34, 197, 94),    # hijau
        (16, 185, 129),   # emerald
        (52, 211, 153),   # teal
        (74, 222, 128),   # lime
    ],
    "FAIL": [
        (239, 68, 68),    # merah
        (220, 38, 38),    # red-dark
        (248, 113, 113),  # rose
        (252, 165, 165),  # red-light
    ],
    "NA": [
        (148, 163, 184),  # slate
    ],
}

LABELS = {
    "PASS": "✓ PASS",
    "FAIL": "✗ FAIL",
}


def make_photo(status: str, item_name: str, index: int) -> bytes:
    """Buat foto JPEG kecil (320×240) dengan warna dan label sesuai status."""
    color_list = COLORS.get(status, COLORS["NA"])
    bg_color = color_list[index % len(color_list)]

    # Warna teks kontras (putih di background gelap/menengah)
    brightness = (bg_color[0] * 299 + bg_color[1] * 587 + bg_color[2] * 114) / 1000
    text_color = (30, 30, 30) if brightness > 150 else (255, 255, 255)

    img = Image.new("RGB", (320, 240), bg_color)
    draw = ImageDraw.Draw(img)

    # Label status besar di tengah atas
    label = LABELS.get(status, status)
    # Ukur teks
    bbox = draw.textbbox((0, 0), label, font=None)
    tw = bbox[2] - bbox[0]
    draw.text(((320 - tw) // 2, 30), label, fill=text_color)

    # Nama item (maks 38 karakter per baris)
    short_name = item_name[:76]
    line1 = short_name[:38]
    line2 = short_name[38:]
    bbox1 = draw.textbbox((0, 0), line1, font=None)
    w1 = bbox1[2] - bbox1[0]
    draw.text(((320 - w1) // 2, 100), line1, fill=text_color)
    if line2:
        bbox2 = draw.textbbox((0, 0), line2, font=None)
        w2 = bbox2[2] - bbox2[0]
        draw.text(((320 - w2) // 2, 120), line2, fill=text_color)

    # Timestamp di bawah
    ts = f"Simulasi — {date.today().strftime('%d/%m/%Y')}"
    bbox3 = draw.textbbox((0, 0), ts, font=None)
    w3 = bbox3[2] - bbox3[0]
    draw.text(((320 - w3) // 2, 200), ts, fill=text_color)

    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=75)
    return buf.getvalue()


# ── HTTP session helpers ──────────────────────────────────────────────────────

class AuditClient:
    def __init__(self, base_url: str, verbose: bool = True):
        self.base = base_url.rstrip("/")
        self.session = requests.Session()
        self.session.headers["User-Agent"] = "AuditSimulator/1.0"
        self.verbose = verbose

    def log(self, msg: str):
        if self.verbose:
            print(msg)

    def login(self, username: str, password: str) -> bool:
        """Login via MVC form — mengambil CSRF token lalu submit form."""
        # 1. GET halaman login untuk anti-forgery token
        r = self.session.get(f"{self.base}/Account/Login", timeout=15)
        if r.status_code != 200:
            print(f"  ERROR: Tidak bisa akses halaman login ({r.status_code})")
            return False

        # Ekstrak __RequestVerificationToken dari form
        m = re.search(r'name="__RequestVerificationToken"[^>]+value="([^"]+)"', r.text)
        if not m:
            print("  ERROR: CSRF token tidak ditemukan di halaman login")
            return False
        token = m.group(1)

        # 2. POST form login
        r2 = self.session.post(
            f"{self.base}/Account/Login",
            data={
                "Username": username,
                "Password": password,
                "__RequestVerificationToken": token,
                "RememberMe": "false",
            },
            allow_redirects=True,
            timeout=15,
        )

        # Cek apakah redirect ke home (bukan balik ke login)
        if "/Account/Login" in r2.url:
            print("  ERROR: Login gagal — username/password salah?")
            return False

        self.log(f"  ✓ Login berhasil sebagai '{username}' (redirect ke {r2.url})")
        return True

    def get_json(self, path: str) -> Optional[dict]:
        r = self.session.get(f"{self.base}{path}", timeout=15)
        r.raise_for_status()
        return r.json()

    def post_json(self, path: str, body: dict) -> dict:
        r = self.session.post(
            f"{self.base}{path}",
            json=body,
            headers={"Content-Type": "application/json"},
            timeout=30,
        )
        r.raise_for_status()
        return r.json()

    def put_json(self, path: str, body: dict) -> requests.Response:
        r = self.session.put(
            f"{self.base}{path}",
            json=body,
            headers={"Content-Type": "application/json"},
            timeout=30,
        )
        r.raise_for_status()
        return r

    def upload_photo(self, path: str, jpeg_bytes: bytes) -> dict:
        r = self.session.post(
            f"{self.base}{path}",
            files={"photo": ("photo.jpg", jpeg_bytes, "image/jpeg")},
            timeout=30,
        )
        r.raise_for_status()
        return r.json()


# ── Simulation logic ──────────────────────────────────────────────────────────

def run(args):
    print("=" * 60)
    print("  HygieneAudit — Simulasi Proses Audit")
    print("=" * 60)
    print(f"  URL    : {args.url}")
    print(f"  User   : {args.username}")
    print()

    client = AuditClient(args.url, verbose=True)

    # ── 1. Login ────────────────────────────────────────────────────────────────
    print("[1] Login...")
    if not client.login(args.username, args.password):
        sys.exit(1)

    # ── 2. Ambil daftar tenant ──────────────────────────────────────────────────
    print("\n[2] Mengambil daftar tenant...")
    tenants = client.get_json("/api/tenants")
    if not tenants:
        print("  ERROR: Tidak ada tenant aktif. Tambahkan tenant terlebih dahulu di menu Tenants.")
        sys.exit(1)

    tenant = tenants[0]
    print(f"  ✓ Menggunakan tenant: {tenant['name']} (id={tenant['id']})")

    # ── 3. Ambil daftar user untuk PIC ─────────────────────────────────────────
    print("\n[3] Mengambil daftar PIC...")
    users = client.get_json("/api/users/auditors")
    if not users:
        print("  ERROR: Tidak ada user aktif untuk PIC. Tambahkan user terlebih dahulu.")
        sys.exit(1)

    # Pilih PIC pertama
    pic = users[0]
    print(f"  ✓ PIC: {pic['name']} (id={pic['id']})")

    # ── 4. Buat Audit baru ──────────────────────────────────────────────────────
    print("\n[4] Membuat audit baru...")
    audit_payload = {
        "date": date.today().isoformat(),
        "tenantId": tenant["id"],
        "picId": pic["id"],
        "isGas": tenant.get("usesGas", False),
    }
    audit = client.post_json("/api/audits", audit_payload)
    audit_id = audit["id"]
    print(f"  ✓ Audit dibuat: ID={audit_id}")
    print(f"    Tenant  : {audit['tenantName']}")
    print(f"    Tanggal : {audit['date'][:10]}")
    print(f"    Tipe    : {'Dengan Gas' if audit['isGas'] else 'Tanpa Gas'}")
    print(f"    Items   : {audit['totalItems']} item checklist")

    if audit["totalItems"] == 0:
        print("\n  ERROR: Audit tidak memiliki item. Pastikan ada template checklist aktif.")
        print("  Tambahkan template di menu Templates, lalu coba lagi.")
        sys.exit(1)

    # ── 5. Ambil detail audit (item list) ───────────────────────────────────────
    print("\n[5] Mengambil detail item checklist...")
    detail = client.get_json(f"/api/audits/{audit_id}")
    items = detail.get("items", [])
    print(f"  ✓ {len(items)} item dimuat dari {len(set(i['category'] for i in items))} kategori")

    # ── 6. Isi setiap item: status + foto ───────────────────────────────────────
    print(f"\n[6] Mengisi {len(items)} item checklist (status + foto)...")
    print()

    pass_count = 0
    fail_count = 0
    photo_count = 0

    # Distribusi: 75% PASS, 25% FAIL (realistis)
    for idx, item in enumerate(items):
        template_id = item["templateId"]
        name = item["name"]
        category = item["category"]

        # Tentukan status (setiap item ke-4 jadi FAIL)
        status = "FAIL" if (idx % 4 == 3) else "PASS"
        note = f"Catatan inspeksi: kondisi {('tidak baik, perlu perbaikan segera' if status == 'FAIL' else 'baik dan sesuai standar')}"

        # Upload foto dulu
        jpeg = make_photo(status, name, idx)
        try:
            photo_result = client.upload_photo(
                f"/api/audits/{audit_id}/items/{template_id}/photos",
                jpeg,
            )
            photo_url = photo_result.get("url", "")
            photo_count += 1
        except Exception as e:
            print(f"  ⚠ Foto gagal untuk '{name}': {e}")
            photo_url = ""

        # Update status item
        update_body = {
            "status": status,
            "note": note if status == "FAIL" else None,
            "photos": [photo_url] if photo_url else [],
        }
        try:
            client.put_json(f"/api/audits/{audit_id}/items/{template_id}", update_body)
            icon = "✓" if status == "PASS" else "✗"
            photo_icon = "📷" if photo_url else "  "
            print(f"  [{idx+1:3d}] {icon} {status:4s} {photo_icon}  [{category}] {name[:50]}")
        except Exception as e:
            print(f"  ⚠ Update gagal untuk '{name}': {e}")
            continue

        if status == "PASS":
            pass_count += 1
        else:
            fail_count += 1

        # Sedikit jeda agar server tidak overwhelmed
        if idx % 10 == 9:
            time.sleep(0.2)

    print()
    print(f"  Selesai: {pass_count} PASS, {fail_count} FAIL, {photo_count} foto diupload")

    # ── 7. Submit Audit ─────────────────────────────────────────────────────────
    print(f"\n[7] Submit audit...")
    try:
        client.session.post(
            f"{client.base}/api/audits/{audit_id}/submit",
            timeout=30,
        ).raise_for_status()
        print("  ✓ Audit berhasil di-submit!")
    except Exception as e:
        print(f"  ERROR submit: {e}")
        sys.exit(1)

    # ── 8. Ambil hasil akhir ────────────────────────────────────────────────────
    print(f"\n[8] Mengambil hasil akhir audit...")
    final = client.get_json(f"/api/audits/{audit_id}")
    total = final["totalItems"]
    passed = final["passCount"]
    failed = final["failCount"]
    rate = round(passed / total * 100) if total > 0 else 0
    status_label = "LULUS" if rate >= 70 else "TIDAK LULUS"

    print()
    print("=" * 60)
    print("  HASIL SIMULASI AUDIT")
    print("=" * 60)
    print(f"  Audit ID   : {audit_id}")
    print(f"  Tenant     : {final['tenantName']}")
    print(f"  PIC        : {final['picName']}")
    print(f"  Tanggal    : {final['date'][:10]}")
    print(f"  Status     : {final['status']}")
    print()
    print(f"  Total item : {total}")
    print(f"  PASS       : {passed}")
    print(f"  FAIL       : {failed}")
    print(f"  Pass Rate  : {rate}%  →  {status_label}")
    print(f"  Foto total : {photo_count}")
    print()
    print(f"  Detail  : {args.url}/Audits/Detail/{audit_id}")
    print(f"  Laporan : {args.url}/Audits/PrintReport/{audit_id}")
    print("=" * 60)


# ── Entry point ───────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(
        description="Simulasi proses audit lengkap dengan foto di setiap item",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Contoh:
  python simulate_audit.py --url http://localhost:44300 --username admin --password Admin123!
  python simulate_audit.py --url https://audit.example.com  --username admin --password secret
        """,
    )
    parser.add_argument("--url",      required=True, help="Base URL aplikasi (e.g. http://localhost:44300)")
    parser.add_argument("--username", required=True, help="Username login (Admin/SuperAdmin)")
    parser.add_argument("--password", required=True, help="Password login")
    args = parser.parse_args()

    try:
        run(args)
    except KeyboardInterrupt:
        print("\nDibatalkan.")
    except requests.HTTPError as e:
        print(f"\nHTTP Error: {e}")
        if e.response is not None:
            try:
                print("Response:", e.response.json())
            except Exception:
                print("Body:", e.response.text[:400])
        sys.exit(1)
    except requests.ConnectionError:
        print(f"\nERROR: Tidak bisa terhubung ke {args.url}")
        print("Pastikan aplikasi sudah berjalan dan URL benar.")
        sys.exit(1)


if __name__ == "__main__":
    main()

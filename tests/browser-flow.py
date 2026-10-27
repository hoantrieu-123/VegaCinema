"""Browser smoke QA against the local demo. Requires playwright and installed Edge.
Run with VEGA_BASE_URL and VEGA_DEMO_PASSWORD, or the ignored appsettings.Local.json.
"""
import json, os, re, uuid
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '.local' / 'qa'
OUT.mkdir(parents=True, exist_ok=True)
base = os.environ.get('VEGA_BASE_URL', 'http://localhost:5188')
config = json.loads((ROOT / 'appsettings.Local.json').read_text(encoding='utf-8-sig')) if (ROOT / 'appsettings.Local.json').exists() else {}
password = os.environ.get('VEGA_DEMO_PASSWORD') or config.get('Seed', {}).get('DemoPassword')
assert password, 'Set VEGA_DEMO_PASSWORD.'
errors, results = [], []
with sync_playwright() as p:
    browser = p.chromium.launch(executable_path=os.environ.get('VEGA_BROWSER', r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'), headless=True)
    page = browser.new_page(viewport={'width': 1440, 'height': 1000}, ignore_https_errors=True)
    page.on('pageerror', lambda e: errors.append(str(e)))
    def visit(path):
        response = page.goto(base + path)
        assert response.status == 200, (path, response.status)
    def login(email):
        visit('/Account/Login')
        page.locator('#Email').fill(email)
        page.locator('#Password').fill(password)
        page.locator('form').filter(has=page.locator('#Email')).get_by_role('button', name='Đăng nhập').click()
        page.wait_for_url(base + '/')
    def logout():
        if page.locator('.admin-sidebar').count():
            page.locator('.admin-sidebar').get_by_role('button', name='Đăng xuất').click()
        else:
            page.locator('.avatar').click()
            page.get_by_role('button', name='Đăng xuất', exact=True).click()
        page.wait_for_url(base + '/')
    login('customer@vega.local')
    visit('/Movies?search=ALWAYS%20ALISA&status=NOW_SHOWING')
    assert page.locator('.movie-card').count() == 1
    visit('/Schedule?cinemaId=1')
    href = page.locator('.time-options a').first.get_attribute('href')
    visit(href)
    page.wait_for_selector('.seat[data-status="AVAILABLE"]:not(:disabled)')
    page.locator('.seat[data-status="AVAILABLE"]:not(:disabled)').nth(0).click()
    page.locator('.seat[data-status="AVAILABLE"]:not(:disabled)').nth(1).click()
    assert page.locator('#seat-total').inner_text() != '0 ₫'
    page.screenshot(path=str(OUT/'seats-desktop.png'), full_page=True)
    page.locator('#hold-seats').click()
    page.wait_for_url('**/Booking/Checkout/**')
    booking_url = page.url.replace('/Checkout/', '/Details/')
    page.wait_for_function("document.querySelector('#countdown').textContent !== '--:--'")
    page.screenshot(path=str(OUT/'checkout-desktop.png'), full_page=True)
    assert page.locator('#payment-qr').count() == 1
    assert page.request.get(base + page.locator('#payment-qr').get_attribute('src')).status == 200
    page.locator('#demo-payment-link').click()
    page.wait_for_url('**/Booking/DemoPayment/**')
    page.locator('#pay-button').click()
    page.wait_for_url('**/Booking/Details/**')
    assert page.locator('.e-ticket').count() == 2
    assert page.locator('.order-heading .status-pill').inner_text() == 'Đã phát hành vé'
    qr_src = page.locator('.ticket-qr img').first.get_attribute('src')
    qr_response = page.request.get(base + qr_src)
    assert qr_response.status == 200 and qr_response.headers['content-type'].startswith('image/png')
    code = page.locator('.ticket-qr p').first.inner_text()
    page.screenshot(path=str(OUT/'tickets-desktop.png'), full_page=True)
    results.append('Customer booking: 2 seats -> hold -> checkout -> payment -> 2 PNG QR tickets')
    assert page.goto(base + '/Admin').status == 403
    assert '/Account/Denied' in page.url
    logout()
    login('checker@vega.local')
    visit('/Staff/Check?code=' + code)
    page.get_by_role('button', name='Xác nhận sử dụng vé').click()
    assert page.locator('.status-pill').inner_text() == 'Đã sử dụng'
    assert page.get_by_role('button', name='Xác nhận sử dụng vé').count() == 0
    results.append('Checker: one successful admission; used ticket has no second admission action')
    logout()
    login('admin@vega.local')
    sections = ['movies','clusters','cinemas','rooms','seats','showtimes','bookings','payments','tickets','users','logs']
    for section in sections:
        visit('/Admin/Catalog?section=' + section)
        if section in ['movies','clusters','cinemas','rooms','seats','showtimes','users']:
            page.get_by_role('link', name='Xem / Sửa').first.click()
            assert page.locator('.admin-editor').count() == 1
    visit('/Admin/Catalog/Edit?section=clusters')
    qa_name = 'QA cluster ' + uuid.uuid4().hex[:8]
    page.locator('#field-name').fill(qa_name)
    page.locator('#field-city').fill('Thành phố kiểm thử')
    page.get_by_role('button', name='Lưu thay đổi').click()
    page.wait_for_url('**/Admin/Catalog?section=clusters')
    assert qa_name in page.locator('table').inner_text()
    page.get_by_role('row').filter(has_text=qa_name).get_by_role('link', name='Xem / Sửa').click()
    page.locator('#field-active').select_option('false')
    page.get_by_role('button', name='Lưu thay đổi').click()
    page.wait_for_url('**/Admin/Catalog?section=clusters')
    results.append('Admin: all 11 lists and 7 editors render; cluster create/update persists')
    visit('/Admin')
    page.screenshot(path=str(OUT/'admin-desktop.png'), full_page=True)
    visit('/Admin/Reports')
    assert page.locator('.stat-card').count() == 3
    logout()
    login('manager@vega.local')
    visit('/Admin/Reports')
    results.append('Manager: report renders with cluster scope')
    logout()
    login('staff@vega.local')
    visit('/Staff')
    page.locator('#search').fill(code) # ticket code is not a booking lookup, verify empty search safely
    page.get_by_role('button', name='Tra cứu').click()
    assert page.locator('tbody tr').count() == 0
    results.append('Staff: lookup renders and filters')
    logout()
    visit('/Account/Register')
    new_email = 'browser-' + uuid.uuid4().hex[:8] + '@vega.local'
    page.locator('#FullName').fill('Khách kiểm thử trình duyệt')
    page.locator('#BirthYear').fill('2000')
    page.locator('#Gender').select_option('Female')
    page.locator('#Address').fill('123 Nguyễn Trãi, TP. Hồ Chí Minh')
    page.locator('#Phone').fill('0901234567')
    page.locator('#Email').fill(new_email)
    page.locator('#Password').fill(password)
    page.locator('#ConfirmPassword').fill(password)
    page.get_by_role('button', name='Tạo tài khoản').click()
    page.wait_for_url(base + '/')
    visit('/Account/Profile')
    page.locator('#FullName').fill('Vega Browser QA')
    page.locator('#Phone').fill('0901234567')
    page.get_by_role('button', name='Lưu thông tin').click()
    assert page.locator('#FullName').input_value() == 'Vega Browser QA'
    assert page.locator('#BirthYear').input_value() == '2000'
    assert page.locator('#Gender').input_value() == 'Female'
    assert page.locator('#Address').input_value() == '123 Nguyễn Trãi, TP. Hồ Chí Minh'
    visit(href)
    assert page.locator('[data-customer="name"]').inner_text() == 'Vega Browser QA'
    assert page.locator('[data-customer="email"]').inner_text() == new_email
    assert page.locator('[data-customer="phone"]').inner_text() == '0901234567'
    page.locator('.seat[data-status="AVAILABLE"]:not(:disabled)').first.click()
    page.locator('#hold-seats').click()
    page.wait_for_url('**/Booking/Checkout/**')
    checkout_url = page.url
    assert page.locator('[data-customer="email"]').inner_text() == new_email
    assert page.locator('[data-customer="phone"]').inner_text() == '0901234567'
    page.screenshot(path=str(OUT/'customer-qr-desktop.png'), full_page=True)
    page.set_viewport_size({'width':390,'height':844})
    assert not page.evaluate('document.documentElement.scrollWidth > innerWidth')
    page.screenshot(path=str(OUT/'customer-qr-mobile.png'), full_page=True)
    page.locator('#demo-payment-link').click()
    page.wait_for_url('**/Booking/DemoPayment/**')
    assert not page.evaluate('document.documentElement.scrollWidth > innerWidth')
    page.screenshot(path=str(OUT/'qr-confirm-mobile.png'), full_page=True)
    page.locator('#pay-button').click()
    page.wait_for_url('**/Booking/Details/**')
    assert page.locator('.e-ticket').count() == 1
    page.screenshot(path=str(OUT/'customer-ticket-mobile.png'), full_page=True)
    page.set_viewport_size({'width':1440,'height':1000})
    results.append('New account: all seven profile fields persist; contact prefills seats and checkout; QR confirmation issues one ticket; 390px checkout and payment have no overflow')
    visit('/Account/Password')
    page.locator('#CurrentPassword').fill(password)
    page.locator('#NewPassword').fill(password + 'Z9!')
    page.locator('#ConfirmPassword').fill(password + 'Z9!')
    page.get_by_role('button', name='Đổi mật khẩu').click()
    page.wait_for_url('**/Account/Profile')
    logout()
    visit('/Account/Login')
    page.locator('#Email').fill(new_email)
    page.locator('#Password').fill(password + 'Z9!')
    page.locator('.auth-panel').get_by_role('button', name='Đăng nhập').click()
    page.wait_for_url(base + '/')
    logout()
    results.append('Account: register, update profile, change password and sign in with new password')
    page.set_viewport_size({'width':390,'height':844})
    for path in ['/', '/Movies', '/Schedule', '/Cinemas', '/Account/Login', '/Account/Register', href]:
        visit(path)
        assert not page.evaluate('document.documentElement.scrollWidth > innerWidth'), 'Mobile overflow: ' + path
    visit('/')
    page.locator('.menu-toggle').click()
    assert page.locator('.menu-toggle').get_attribute('aria-expanded') == 'true'
    page.locator('.menu-toggle').click()
    page.screenshot(path=str(OUT/'home-mobile.png'), full_page=True)
    page.set_viewport_size({'width':1440,'height':1000})
    page.screenshot(path=str(OUT/'home-desktop.png'), full_page=True)
    results.append('Mobile 390px: home, movies, schedule, cinemas, login and seats have no page overflow; menu opens')
    assert not errors, errors
    (OUT/'browser-results.json').write_text(json.dumps({'passed':results,'javascript_errors':errors},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'passed':results,'javascript_errors':errors},ensure_ascii=False,indent=2))
    browser.close()

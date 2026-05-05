// SmartStudy universal AJAX helper — HW3 pattern
//
// Usage from a page:
//   var server = "http://localhost:5071/";        // or production URL
//   var api    = server + "api/auth/login";
//   ajaxCall("POST", api, JSON.stringify(body), successCB, errorCB);
//
// JWT auth header injection and 401 auto-logout are wired up globally by
// appShell.js (via $.ajaxSetup and $(document).ajaxError), so this wrapper
// stays bare.

// Universal jQuery $.ajax wrapper used by every page for JSON API calls.
function ajaxCall(method, api, data, successCB, errorCB) {
    $.ajax({
        type: method,
        url: api,
        data: data,
        cache: false,
        contentType: "application/json; charset=utf-8",
        success: successCB,
        error: errorCB
    });
}

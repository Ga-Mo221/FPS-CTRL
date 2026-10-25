// Eclipse Strike — landing page: menu mobile, slider ảnh, chọn đặc vụ, thanh tải dính đáy, hiệu ứng cuộn.
(function () {
  "use strict";

  // ---------- Menu mobile ----------
  var toggle = document.getElementById("navToggle");
  var nav = document.getElementById("nav");
  if (toggle && nav) {
    toggle.addEventListener("click", function () {
      var open = nav.classList.toggle("is-open");
      toggle.setAttribute("aria-expanded", open ? "true" : "false");
    });
    nav.addEventListener("click", function (e) {
      if (e.target.tagName === "A") {
        nav.classList.remove("is-open");
        toggle.setAttribute("aria-expanded", "false");
      }
    });
  }

  // ---------- Slider ảnh trong game ----------
  document.querySelectorAll("[data-slider]").forEach(function (root) {
    var slides = Array.prototype.slice.call(root.querySelectorAll(".slide"));
    var dotsWrap = root.querySelector(".slider__dots");
    var current = root.querySelector("[data-current]");
    var total = root.querySelector("[data-total]");
    var index = 0, timer = null;
    var pad = function (n) { return (n < 10 ? "0" : "") + n; };
    if (total) total.textContent = pad(slides.length);

    var dots = slides.map(function (_, i) {
      var b = document.createElement("button");
      b.type = "button";
      b.setAttribute("role", "tab");
      b.setAttribute("aria-label", "Ảnh " + (i + 1));
      b.addEventListener("click", function () { go(i); restart(); });
      dotsWrap.appendChild(b);
      return b;
    });

    function go(i) {
      index = (i + slides.length) % slides.length;
      slides.forEach(function (s, k) { s.classList.toggle("is-active", k === index); });
      dots.forEach(function (d, k) { d.setAttribute("aria-selected", k === index ? "true" : "false"); });
      if (current) current.textContent = pad(index + 1);
    }
    function restart() {
      clearInterval(timer);
      timer = setInterval(function () { go(index + 1); }, 5000);
    }
    root.querySelector("[data-prev]").addEventListener("click", function () { go(index - 1); restart(); });
    root.querySelector("[data-next]").addEventListener("click", function () { go(index + 1); restart(); });
    root.addEventListener("mouseenter", function () { clearInterval(timer); });
    root.addEventListener("mouseleave", restart);
    go(0);
    restart();
  });

  // ---------- Đặc vụ ----------
  var OPERATORS = {
    ghost: {
      name: "GHOST",
      role: "ĐẶC VỤ BÓNG ĐÊM",
      img: "assets/op-ghost.png",
      desc: "Lạnh lùng, bí ẩn. Mặt nạ đầu lâu và kính nhìn đêm — Ghost xuất hiện ở nơi đối thủ ít ngờ nhất và rút lui trước khi ai kịp phản ứng.",
      traits: [["Phong cách", "Đột nhập, đánh úp"], ["Hợp với", "Umbra · Vesper"], ["Mẹo", "Ném M84 Flash trước khi vào góc"]]
    },
    olivia: {
      name: "OLIVIA",
      role: "TAY SÚNG LINH HOẠT",
      img: "assets/op-olivia.png",
      desc: "Nhanh, quyết đoán. Olivia đổi vị trí liên tục, ép đối thủ vào thế phải phản ứng và kết thúc pha giao tranh trước khi chúng kịp ổn định tâm ngắm.",
      traits: [["Phong cách", "Cơ động, áp sát"], ["Hợp với", "Comet · Umbra"], ["Mẹo", "Trượt người để vào góc hẹp nhanh hơn"]]
    },
    anhteo: {
      name: "ANH TÈO",
      role: "XẠ THỦ KIÊN CƯỜNG",
      img: "assets/op-anhteo.png",
      desc: "Mũ tai bèo, khăn rằn và đôi dép quen thuộc. Anh Tèo kiên nhẫn giữ góc, chờ đúng khoảnh khắc để hạ gục chỉ bằng một phát Dusk.",
      traits: [["Phong cách", "Giữ góc, bắn tỉa"], ["Hợp với", "Dusk · Vesper"], ["Mẹo", "Dùng AN-M8 Smoke để đổi vị trí an toàn"]]
    }
  };

  var opRoot = document.querySelector("[data-operators]");
  if (opRoot) {
    var img = opRoot.querySelector("[data-op-img]");
    var nameEl = opRoot.querySelector("[data-op-name]");
    var roleEl = opRoot.querySelector("[data-op-role]");
    var descEl = opRoot.querySelector("[data-op-desc]");
    var traitsEl = opRoot.querySelector("[data-op-traits]");
    var thumbs = opRoot.querySelectorAll(".op-thumb");

    var showOperator = function (key) {
      var op = OPERATORS[key];
      if (!op) return;
      thumbs.forEach(function (t) {
        var on = t.getAttribute("data-op") === key;
        t.classList.toggle("is-active", on);
        t.setAttribute("aria-selected", on ? "true" : "false");
      });
      img.classList.add("is-swapping");
      setTimeout(function () {
        img.src = op.img;
        img.alt = "Đặc vụ " + op.name;
        nameEl.textContent = op.name;
        roleEl.textContent = op.role;
        descEl.textContent = op.desc;
        traitsEl.innerHTML = "";
        op.traits.forEach(function (t) {
          var li = document.createElement("li");
          var b = document.createElement("b"); b.textContent = t[0];
          var s = document.createElement("span"); s.textContent = t[1];
          li.appendChild(b); li.appendChild(s);
          traitsEl.appendChild(li);
        });
        img.classList.remove("is-swapping");
      }, 220);
    };
    thumbs.forEach(function (t) {
      t.addEventListener("click", function () { showOperator(t.getAttribute("data-op")); });
    });
    // Tải trước ảnh đặc vụ để đổi không bị nháy
    Object.keys(OPERATORS).forEach(function (k) { var i = new Image(); i.src = OPERATORS[k].img; });
  }

  // ---------- Hiện dần khi cuộn ----------
  var reveals = document.querySelectorAll(".reveal");
  if ("IntersectionObserver" in window) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (e.isIntersecting) { e.target.classList.add("is-in"); io.unobserve(e.target); }
      });
    }, { threshold: 0.15, rootMargin: "0px 0px -40px 0px" });
    reveals.forEach(function (el, i) {
      el.style.transitionDelay = (i % 5) * 60 + "ms";
      io.observe(el);
    });
  } else {
    reveals.forEach(function (el) { el.classList.add("is-in"); });
  }

  // ---------- Thanh tải dính đáy + đánh dấu mục menu ----------
  var dock = document.getElementById("dock");
  var hero = document.querySelector(".hero");
  var download = document.getElementById("tai-game");
  var links = document.querySelectorAll(".nav a");
  var sections = Array.prototype.map.call(links, function (a) { return document.querySelector(a.getAttribute("href")); });

  var onScroll = function () {
    var y = window.scrollY;
    if (dock && hero && download) {
      var pastHero = y > hero.offsetHeight * 0.7;
      var atDownload = download.getBoundingClientRect().top < window.innerHeight * 0.8;
      var show = pastHero && !atDownload;
      dock.classList.toggle("is-visible", show);
      dock.setAttribute("aria-hidden", show ? "false" : "true");
      dock.querySelector("a").tabIndex = show ? 0 : -1;
    }
    var active = -1;
    sections.forEach(function (s, i) {
      if (s && s.getBoundingClientRect().top < window.innerHeight * 0.4) active = i;
    });
    links.forEach(function (a, i) { a.classList.toggle("is-active", i === active); });
  };
  window.addEventListener("scroll", onScroll, { passive: true });
  onScroll();
})();

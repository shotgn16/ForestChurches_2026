$(document).ready(function () {
    var videoHeight = $('.hero-container video').outerHeight();
    $(window).scroll(function () {
        if ($(this).scrollTop() > videoHeight) {
            $('#mainNavbar').addClass('scrolled');
        } else {
            $('#mainNavbar').removeClass('scrolled');
        }
    });
});

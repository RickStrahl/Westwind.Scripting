/// <reference path="jquery/jquery.js" />
/// <reference path="ww.jquery.js" />
/// <reference path="highlightjs/highlight.pack.js" />

// global page reference
window.helpBuilder = null;

(function () {
    // interface
    helpBuilder = {
        initializeLayout: initializeLayout,
        initializeTOC: initializeTOC,
        isLocalUrl: isLocalUrl,        
        expandTopic: expandTopic,        
        expandParent: expandParents,
        tocExpandAll: tocExpandAll,
        tocExpandTop: tocExpandTop,
        tocCollapseAll: tocCollapseAll,
        tocClearSearchBox: tocClearSearchBox,
        highlightCode:  highlightCode,
        updateDocumentOutline: updateDocumentOutline,
        refreshDocument: refreshDocument,
        configureAceEditor: null // set in aceConfig
    };  
   

    function initializeLayout(notused) {        
       // for old IE versions work around no FlexBox
        if (navigator.userAgent.indexOf("MSIE 9") > -1 ||
	        navigator.userAgent.indexOf("MSIE 8") > -1 || 
	        navigator.userAgent.indexOf("MSIE 7") > -1)
            $(document.body).addClass("old-ie");

        // modes: none/0 - with sidebar,  1 no sidebar
        var mode = getUrlEncodedKey("mode");
        if (mode)
            mode = mode * 1;
        else
            mode = 0;

        // Legacy processing page=TopicId urls to load topic by id
        var page = getUrlEncodedKey("page");
        if (page)
            loadTopicAjax(page);

        var isLocal = isLocalUrl();

        $(".page-content").on("click", "a", function (e) {            
            var href = $(this).attr("href");                

            // ajax navigation online
            if (!isLocal && href.startsWith("_")) {      
                loadTopicAjax(href);
                return false; // stop navigation
            } 
            // external links open in new window
            if ( href.startsWith("http") )
            {
                window.open(href,"_blank");
                return false;
            }

            return true;
        });



        if (!isLocal){
	        // load internal help links via Ajax
	        $(".page-content").on("click", "a", function (e) {            
                var href = $(this).attr("href");                
	            if (href.startsWith("_")) {      
	                loadTopicAjax(href);
	                return false; // stop navigation
	            } 
	        });

            var id = getIdFromUrl();
            if (id){
                setTimeout(function() {
                    $(".toc li a").removeClass("selected");
                    var $a = $("#" + id);
                    $a.addClass("selected");
                    if ($a.length > 0)                    
                        $a[0].scrollIntoView(); 
                },100);
            }
    	}

        if (isLocalUrl() || mode === 1) {
            hideSidebar();                        
        } else {
            $.get("tableofcontents.htm", loadTableOfContents);

            // sidebar or hamburger click handler
            $(document.body).on("click", ".sidebar-toggle", toggleSidebar);
            $(document.body).on("dblclick touchend", ".splitter", toggleSidebar);
             
            $(".sidebar-left").resizable({
                handleSelector: ".splitter",
                resizeHeight: false
            });

            // handle back/forward navigation so URL updates
            window.onpopstate = function (event) {                
                if (history.state && history.state.URL)
                    loadTopicAjax(history.state.URL,true);
            }             
            
        }

        timeToRead();
        
        setTimeout(function() {
            helpBuilder.refreshDocument();
            $(".main-content").scroll(debounce(scrollSpy,100));
            scrollSpy();
        },10);
    }

    var sidebarTappedTwice = false;
    function toggleSidebar(e) {

        // handle double tap
        if (e.type === "touchend" && !sidebarTappedTwice) {
            sidebarTappedTwice = true;
            setTimeout(function () { sidebarTappedTwice = false; }, 300);
            return false;
        }
        var $sidebar = $(".sidebar-left");
        var oldTrans = $sidebar.css("transition");
        $sidebar.css("transition", "width 0.5s ease-in-out");
        if ($sidebar.width() < 20) {
            $sidebar.show();
            $sidebar.width(400);
        } else {
            $sidebar.width(0);
        }

        setTimeout(function () { $sidebar.css("transition", oldTrans) }, 700);
        return true;
    }

    function loadTableOfContents(html) {
        var $tocContent = $("<div>" + getBodyFromHtmlDocument(html) + "</div>").find(".toc-content");
        $("#toc").html($tocContent.html());

        showSidebar();

        // handle AJAX loading of topics        
        $(".toc").on("click", "li a", loadTopicAjax);

        initializeTOC();

        $("#SearchBox").focus();
        return false;
    }
    function loadTopicAjax(href, noPushState) {
        var hrefPassed = true;
        
        if(window.innerWidth < 768)
            hideSidebar();

        if (typeof href != "string") {
            var $a = $(this);
            href = $a.attr("href");
            hrefPassed = false;
            
            $(".toc li a").removeClass("selected");
            $a.addClass("selected");   
        }

        if ($(this).parent().find("i.fa").length > 0)
            expandTopic(href);


        // ajax navigation
        if (href.startsWith("_")) {
            $.get(href, function (html) {
                var $html = $(html);

                var title = html.extract("<title>", "</title>");
                window.document.title = title;

                var $content = $html.find(".main-content");
                if ($content.length > 0) {
                    html = $content.html();
                    $(".main-content").html(html);                    

                    // update the navigation history/url in addressbar
                    if (window.history.pushState && href.startsWith('_'))
                        if (!noPushState)  window.history.pushState({ title: '', URL: href }, "", href);
                    else
                        expandParents(href.replace(".htm", ""));

                    $(".main-content").scrollTop(0);
                } else
                    return;

                var $banner = $html.find(".banner");
                if ($banner.length > 0);
                $(".banner").html($banner.html());

                helpBuilder.refreshDocument();

                $(".main-content").scroll(debounce(scrollSpy,100));
                scrollSpy();
            });
            return false;  // don't allow click
        }
        return true;  // pass through click
    }; 
    function initializeTOC() {

        // if running in frames mode link to target frame and change mode
        if (window.parent.frames["wwhelp_right"]) {
            $(".toc li a").each(function () {
                var $a = $(this);
                $a.attr("target", "wwhelp_right");
                var a = $a[0];
                a.href = a.href + "?mode=1";
            });
            $("ul.toc").css("font-size", "1em");
        }

        // Handle clicks on + and -
        $("#toc").on("click","li>i.fa",function () {            
            expandTopic($(this).find("~a").prop("id") );                        
        });
        $("#toc").on("click","#SearchBoxClearButton",helpBuilder.tocClearSearchBox);

        expandTopic('index');

        var page = getUrlEncodedKey("page");
        if (page) {
            page = page.replace(/.htm/i, "");
            expandParents(page);
        }
        if (!page) {
            page = window.location.href.extract("/_", ".htm");
            if (page)
                expandParents("_" + page);
        }

        var topic = getUrlEncodedKey("topic");
        if (topic) {
            var id = findIdByTopic();
            if (id) {
                var link = document.getElementById(id);
                var id = link.id;
                expandTopic(id);
                expandParents(id);
                loadTopicAjax(id + ".htm");
            }
        }

        function searchFilterFunc(target) {
            target.each(function () {
                var $a = $(this).find(">a");
                if ($a.length > 0) {
                    var url = $a.attr('href');
                    if (!url.startsWith("file:") && !url.startsWith("http")) {
                        expandParents(url.replace(/.htm/i, ""), true);
                    }
                }

                // keep selected item in the view when removing filter
                setTimeout(function() {
                    var $sel = $(".toc .selected");

                    if ($sel.length > 0)
                        $sel[0].scrollIntoView();    
                },200);
                    
            });
        }

        $("#SearchBox").searchFilter({
            targetSelector: ".toc li",
            charCount: 3,
            onSelected: debounce(searchFilterFunc, 300)
        });
    }

    function hideSidebar() {
        var $sidebar = $(".sidebar-left");
        var $toggle = $(".sidebar-toggle");
        var $splitter = $(".splitter");
        $sidebar.hide();
        $toggle.hide();
        $splitter.hide();
    }
    function showSidebar() {
        var $sidebar = $(".sidebar-left");
        var $toggle = $(".sidebar-toggle");
        var $splitter = $(".splitter");
        $sidebar.show();
        $toggle.show();
        $splitter.show();
    }
    
    function expandTopic(topicId) {        
        var $href = $("#" + topicId.replace(".htm", ""));

        var $ul = $href.next();
        $ul.toggle();

        var $button = $href.prev().prev();

        if ($ul.is(":visible"))
            $button.removeClass("fa-caret-right").addClass("fa-caret-down");
        else
            $button.removeClass("fa-caret-down").addClass("fa-caret-right");
    }

    function expandParents(id, noFocus) {
        if (!id)
            return;

        var $node = $("#" + id.toLowerCase());
        $node.parents("ul").show();

        if (noFocus)
            return;

        var node = $node[0];
        if (!node)
            return;

        //node.scrollIntoView(true);
        node.focus();
        setTimeout(function () {
            window.scrollX = 0;
        });

    }
    function findIdByTopic(topic) {
        if (!topic) {
            var query = window.location.search;
            var match = query.search("topic=");
            if (match < 0)
                return null;
            topic = query.substr(match + 6);
            topic = decodeURIComponent(topic);
        }
        var id = null;
        $("a").each(function () {
            if ($(this).text().toLowerCase() == topic.toLocaleLowerCase()) {
                id = this.id;
                return;
            }
        });
        return id;
    }
    
    function tocClearSearchBox() { 
        $("#SearchBox").val("").focus();

        // make all visible
        $(".toc li").show();

        // make sure we preserve selection
        var $el = $(".selected");        
        if ($el.length > 0) {
            setTimeout(function() { 
                $el[0].scrollIntoView();
                setTimeout(function() {
                    var sh = $("#toc")[0].scrollTop;                        
                    $("#toc")[0].scrollTop = sh - 45; 
                },20);                    
            },5);
        }
    }

    function tocCollapseAll() {

        $("ul.toc > li ul:visible").each(function () {
            var $el = $(this);
            var $href = $el.prev();
            var id = $href[0].id;
            expandTopic(id);
        });
    }

    function tocExpandAll() {
        $("ul.toc > li ul:not(:visible)").each(function () {
            var $el = $(this);
            var $href = $el.prev();
            var id = $href[0].id;
            expandTopic(id);
        });
    }
    function tocExpandTop() {        
        $("ul.toc>li>ul:not(:visible)").each(function () {
            var $el = $(this);
            var $href = $el.prev();
            var id = $href[0].id;
            expandTopic(id);
        });
    }
    function isLocalUrl(href) {
        if (!href)
            href = window.location.href;

        return href.startsWith("mk:@MSITStore") ||
	           href.startsWith("file://")
    }
    function getIdFromUrl(href) {
        if (!href)
            href = window.location.href;

        if(!href.startsWith("_")) {
            href = href.extract("/_", ".htm");
            if(href)
                href = "_" + href;
        }
        
        if (href.startsWith("_"))
            return href.toLowerCase().replace(".htm","");
        
        return null;
    }
    function mtoParts(address, domain, query) {
        var url = "ma" + "ilto" + ":" + address + "@" + domain;
        if (query)
            url = url + "?" + query;
        return url;
    }


    function highlightCode() {   
        var pres = document.querySelectorAll("pre>code");
        for (var i = 0; i < pres.length; i++) {
            hljs.highlightBlock(pres[i]);
        }
    }

    function CreateHeaderLinks() {
        var $h3 = $(".content-body>h2,.content-body>h3,.content-body>h4,.content-body>h1");

        $h3.each(function () {            
            var $h3item = $(this);
            $h3item.css("cursor", "pointer");

            var tag = $h3item[0].id; //text().replace(/\s+/g, "");

            var $a = $("<a />")
	            .attr({
	                name: tag,
	                href: "#" + tag
	            })
	            .addClass('link-icon')
	            .addClass('link-hidden')
                .attr('title', 'click this link and set the bookmark url in the address bar.');

            $h3item.prepend($a);

            $h3item
	            .hover(
	                function () {
	                    $a.removeClass("link-hidden");
	                },
	                function () {
	                    $a.addClass("link-hidden");
	                })
	            .click(function () {
	                window.location = $a.prop("href");
	            });
        });

        if(location.hash){
            setTimeout(function() {
                // navigate # links if any            

                var hash = location.hash.replace("#","");
                var el$ = $(location.hash+ ",a[name=" + hash + "]");            
                if (el$.length > 0)
                {
                    el$[0].scrollIntoView(true);
                    var mc$ = $(".main-content");                
                    mc$[0].scrollTop = mc$[0].scrollTop - 80;
                }
            });
        }
    }

    function updateDocumentOutline(){
        var navbar$ = $(".topic-outline-content");                       
        navbar$.html("");

        var headers$ = $(".content-pane").find("h1,h2,h3,h4");
        
        if (headers$.length < 2)
        {                   
            $(".content-pane").removeClass("topic-outline-visible");
            $(".topic-outline-header").hide(false);
            return;
        }                

        for (var index = 0; index < headers$.length; index++) {
            var el = headers$[index];
            var id = el.id;
            if (!id) {
                el.id = safeId(el.innerText);
                id = el.id
            }
                
            var space = "";
            if (el.nodeName == "H1")
                space = "outline-level1";
            else if (el.nodeName == "H2")
                space = "outline-level2";
            else if (el.nodeName == "H3")
                space = "outline-level3";
            else if (el.nodeName == "H4")
                space = "outline-level4";
            var a$ = $("<a></a>")
                .prop("href", "#" + id)
                .text(el.innerText);
            if (space)
                a$.addClass(space);

            navbar$.append(a$);
        } 

        $(".content-pane").addClass("topic-outline-visible");
        $(".topic-outline-header").show(true);                    
    }

    function scrollSpy() {        
        var headers$ = $(".topic-outline-content>a");        
        if(headers$.length < 1)
            return;

        for (var index = 0; index < headers$.length; index++) {
            const hd$ = $(headers$[index]);
            var id = hd$.attr('href');

            var id$;
            try{
                 id$ = $(id);
            }catch(ex) {
                continue;
            }
            if(id$.length < 1)
                continue;

            if(id$.isInViewport())
            {                
                $(".topic-outline-content *").removeClass("active");
                hd$.addClass("active");
                break;
            }
        }
    }

    $.fn.isInViewport = function() {
        var elementTop = $(this).offset().top;
        var elementBottom = elementTop + $(this).outerHeight();
        var viewportTop = $(window).scrollTop();
        var viewportBottom = viewportTop + $(window).height();
        return elementBottom > viewportTop && elementTop < viewportBottom;
    };   

    function safeId(inputString) {
        if (!inputString) return inputString;
       var id =  $.trim(inputString)
            .replace(/-/g,"--")
            .replace(/[\s,-,:,.,\',\",\\,/,(,),#<,$,%,@,!,*']/g,"-");
        return id;               
    }

    /* 
        Updates the document with post-processing scripts.
        Called when page reloads.
    */
    function refreshDocument() {
        helpBuilder.highlightCode();
        CreateHeaderLinks();
        helpBuilder.updateDocumentOutline();    
    }
    
    function timeToRead() {
        ttr$ = $("#TimeToRead");
        if (ttr$.length == 0)
            return;

        var wordsPerMinute = 250;
        var content = $('.content-pane').text();

        var regExWords = /\s+/gi;
        var wordCount = content.replace(regExWords, ' ').split(' ').length;

        // while (content.indexOf('  ') > -1)
        //     content = content.replace('  ',' ');

        // var words = content.split(' ');
        // var wordCount = words.length;
        var readingTimeText = '';
        
        if (wordCount > wordsPerMinute) {
            var minutes = wordCount / wordsPerMinute;
            minutes = minutes.toFixed(0);
            if (minutes == 1) readingTimeText = 'about 1 minute to read';
            else if (minutes < 9) readingTimeText = 'about ' + minutes + ' minutes to read';
            else if (minutes < 13) readingTimeText = 'about 10 minutes to read';
            else if (minutes < 18) readingTimeText = 'about 15 minutes to read';
            else if (minutes < 23) readingTimeText = 'about 20 minutes to read';
            else if (minutes < 28) readingTimeText = 'about 25 minutes to read';
            else if (minutes < 38) readingTimeText = 'about a half hour to read';
            else if (minutes < 50) readingTimeText = 'about 45 minutes to read';
            else if (minutes < 70) readingTimeText = 'about one hour to read';
            else if (minutes < 80) readingTimeText = 'about an hour and 15 minutes to read';
            else if (minutes < 100) readingTimeText = 'about an hour and a half to read';
            else if (minutes < 130) readingTimeText = 'about two hours to read';
            else if (minutes < 160) readingTimeText = 'about two and a half hours to read';
            else if (minutes < 190) readingTimeText = 'about three hours to read';
            else readingTimeText = 'more than three hours to read';
        }
        else if (wordCount > 150) {
            readingTimeText = 'less than 1 minute to read';
        }

        if (readingTimeText.length > 0)
            ttr$.html('<span><i class="fa fa-clock-o"></i> '+ readingTimeText +'</span>');
    }    

})();



// global functions called from HelpBuilder Dev
function updatedocumentcontent(html,pragmaLine) {
      
        $(".main-content").html(html);

        // refresh syntax coloring and header links
        helpBuilder.refreshDocument();
                    
        if (typeof pragmaLine === "number")
            scrolltopragmaline(pragmaLine);
}

function scrolltopragmaline(lineno) {             
    if (typeof lineno != "number")   
        return;
    
    $mc = $(".main-content");               
    if (lineno < 2){
            $mc[0].scrollTop = 0;
            return;
    }

    try {
        var $el = $("#pragma-line-" + lineno);
                    
        if ($el.length < 1) {
            var origLine = lineno;
            for (var i = 0; i < 3; i++) {
                lineno++;
                $el = $("#pragma-line-" + lineno);
                if ($el.length > 0)
                    break;
            }
            if ($el.length < 1) {
                lineno = origLine;
                for (var i = 0; i < 3; i++) {
                    lineno--;
                    $el = $("#pragma-line-" + lineno);
                    if ($el.length > 0)
                        break;
                }
            }
            if ($el.length < 1)
                return;
        }
        
        $el.addClass("line-highlight");
        setTimeout(function() { $el.removeClass("line-highlight"); }, 1200);

        setTimeout(function() {
            $el[0].scrollIntoView(); 
            if (lineno > 2)                             
                $mc[0].scrollTop = $mc[0].scrollTop - 80;                                   
        });
        
        
    }
    catch(ex) {  
        
    }       
}
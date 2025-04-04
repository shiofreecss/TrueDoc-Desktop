
    const icongen = require('icon-gen');
    
    icongen('document_icon.png', './', {
      report: true,
      ico: {
        name: 'document_icon',
        sizes: [16, 24, 32, 48, 64, 128, 256]
      }
    })
    .then((results) => {
      console.log('Generated:', results);
    })
    .catch((err) => {
      console.error(err);
    });
    